"""Golden-set evaluation runner.

Runs the real `run_grading_pipeline` against lecturer-graded papers and reports accuracy metrics.
See eval/README.md for the fixture format and the metric targets (doc section 9).

Usage (from core/src/Services/AIGradingService, venv active, OPENAI_API_KEY set):

    python -m eval.run_golden
    python -m eval.run_golden eval/golden/example_subject.json --samples 3 --out eval/reports
"""

from __future__ import annotations

import argparse
import asyncio
import functools
import glob
import json
import os
import re
import statistics
import sys
import tempfile
import threading
from dataclasses import dataclass, field
from datetime import datetime, timezone
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from typing import Any

# Make `app` importable when run as a module from the service root.
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.schemas.grading import GradePaperRequest, QuestionSuggestion, ScoreGridItem  # noqa: E402
from app.services.pipeline import PipelineError, run_grading_pipeline  # noqa: E402

TOLERANCE = 0.25  # within-tolerance band (doc section 9)


# ---------------------------------------------------------------------------
# Local static file server (so the real download/normalize path runs)
# ---------------------------------------------------------------------------

def _start_file_server(root: str) -> tuple[ThreadingHTTPServer, str]:
    handler = functools.partial(SimpleHTTPRequestHandler, directory=root)
    httpd = ThreadingHTTPServer(("127.0.0.1", 0), handler)
    thread = threading.Thread(target=httpd.serve_forever, daemon=True)
    thread.start()
    host, port = httpd.server_address
    return httpd, f"http://{host}:{port}"


# ---------------------------------------------------------------------------
# Metrics
# ---------------------------------------------------------------------------

def _normalize(text: str) -> str:
    return re.sub(r"\s+", " ", text or "").strip().lower()


def _is_grounded(evidence: list[str], answer: str) -> bool:
    """True if at least one evidence string appears (softly) in the answer."""
    norm_answer = _normalize(answer)
    for ev in evidence or []:
        norm_ev = _normalize(ev)
        if not norm_ev:
            continue
        if norm_ev in norm_answer:
            return True
        # word-overlap fallback
        ev_words = set(norm_ev.split())
        ans_words = set(norm_answer.split())
        if ev_words and len(ev_words & ans_words) / len(ev_words) >= 0.6:
            return True
    return False


@dataclass
class Accumulator:
    abs_errors: list[float] = field(default_factory=list)
    within_tol: int = 0
    scored_criteria: int = 0
    expected_criteria: int = 0
    returned_criteria: int = 0
    grounding_ok: int = 0
    grounding_total: int = 0
    manual_only: int = 0
    total_suggestions: int = 0
    agreements: list[float] = field(default_factory=list)
    papers: int = 0
    errors: list[str] = field(default_factory=list)

    def report(self) -> dict[str, Any]:
        mae = statistics.mean(self.abs_errors) if self.abs_errors else None
        within = (self.within_tol / self.scored_criteria) if self.scored_criteria else None
        coverage = (self.returned_criteria / self.expected_criteria) if self.expected_criteria else None
        grounding = (self.grounding_ok / self.grounding_total) if self.grounding_total else None
        manual_rate = (self.manual_only / self.total_suggestions) if self.total_suggestions else None
        agreement = statistics.mean(self.agreements) if self.agreements else None
        return {
            "papers": self.papers,
            "mae_per_criterion": round(mae, 4) if mae is not None else None,
            "within_tolerance_pct": round(100 * within, 1) if within is not None else None,
            "coverage_pct": round(100 * coverage, 1) if coverage is not None else None,
            "evidence_grounding_pct": round(100 * grounding, 1) if grounding is not None else None,
            "manual_only_pct": round(100 * manual_rate, 1) if manual_rate is not None else None,
            "inter_sample_agreement": round(agreement, 4) if agreement is not None else None,
            "targets": {
                "mae_per_criterion": "<= 0.2",
                "within_tolerance_pct": ">= 90",
                "coverage_pct": "100",
                "evidence_grounding_pct": ">= 99",
                "manual_only_pct": "<= 15",
                "inter_sample_agreement": ">= 0.85",
            },
            "errors": self.errors,
        }


def _sample_agreement(scores: list[float]) -> float:
    """Fraction of samples whose score is within TOLERANCE of the median (1.0 when N<2)."""
    if len(scores) < 2:
        return 1.0
    med = statistics.median(scores)
    close = sum(1 for s in scores if abs(s - med) <= TOLERANCE)
    return close / len(scores)


# ---------------------------------------------------------------------------
# Runner
# ---------------------------------------------------------------------------

async def _grade_paper(paper: dict, subject_id: str, rubric_version: str,
                       rubric_text: str | None, base_url: str, files_dir: str,
                       samples: int) -> list[list[QuestionSuggestion]]:
    """Grade one paper `samples` times; return one suggestion-list per sample."""
    answer_name = f"{paper['assignment_id']}.txt"
    with open(os.path.join(files_dir, answer_name), "w", encoding="utf-8") as fh:
        fh.write(paper["answer_text"])

    score_grid = [ScoreGridItem.model_validate(item) for item in paper["score_grid"]]
    req = GradePaperRequest.model_validate({
        "assignmentId": paper["assignment_id"],
        "subjectId": subject_id,
        "rubricVersion": rubric_version,
        "files": [{"url": f"{base_url}/{answer_name}", "contentType": "text/plain"}],
        "scoreGrid": [item.model_dump(by_alias=True) for item in score_grid],
        "rubricText": rubric_text,
    })

    runs: list[list[QuestionSuggestion]] = []
    for _ in range(samples):
        result = await run_grading_pipeline(req)
        runs.append(result.suggestions)
    return runs


def _accumulate(acc: Accumulator, paper: dict, runs: list[list[QuestionSuggestion]]) -> None:
    expected: dict[str, float] = {str(k): float(v) for k, v in paper.get("expected", {}).items()}
    acc.papers += 1
    acc.expected_criteria += len(expected)

    # Use the first sample for point metrics; all samples for agreement.
    primary = runs[0]
    by_q: dict[str, list[QuestionSuggestion]] = {}
    for sample in runs:
        for s in sample:
            by_q.setdefault(s.question_number, []).append(s)

    acc.returned_criteria += len({s.question_number for s in primary})
    acc.total_suggestions += len(primary)

    answer = paper["answer_text"]
    for s in primary:
        if "manual_only" in s.flags:
            acc.manual_only += 1
        acc.grounding_total += 1
        if _is_grounded(s.evidence, answer):
            acc.grounding_ok += 1

    for qn, exp in expected.items():
        matches = [s for s in primary if s.question_number == qn]
        if not matches:
            continue
        err = abs(matches[0].score - exp)
        acc.abs_errors.append(err)
        acc.scored_criteria += 1
        if err <= TOLERANCE:
            acc.within_tol += 1

    if len(runs) > 1:
        for qn in expected:
            scores = [s.score for s in by_q.get(qn, [])]
            if scores:
                acc.agreements.append(_sample_agreement(scores))


async def _run(fixtures: list[str], samples: int) -> Accumulator:
    acc = Accumulator()
    files_dir = tempfile.mkdtemp(prefix="golden_answers_")
    httpd, base_url = _start_file_server(files_dir)
    try:
        for path in fixtures:
            with open(path, encoding="utf-8") as fh:
                fixture = json.load(fh)
            subject_id = fixture["subject_id"]
            rubric_version = str(fixture.get("rubric_version", "1"))
            rubric_text = fixture.get("rubric_text")
            for paper in fixture.get("papers", []):
                try:
                    runs = await _grade_paper(
                        paper, subject_id, rubric_version, rubric_text,
                        base_url, files_dir, samples,
                    )
                    _accumulate(acc, paper, runs)
                except (PipelineError, Exception) as exc:  # noqa: BLE001
                    acc.errors.append(f"{paper.get('assignment_id')}: {exc}")
    finally:
        httpd.shutdown()
    return acc


def main() -> None:
    parser = argparse.ArgumentParser(description="Run the AI grading golden-set eval.")
    parser.add_argument("fixtures", nargs="*", help="Fixture JSON files (default: eval/golden/*.json)")
    parser.add_argument("--samples", type=int, default=1, help="Runs per paper (>=2 enables agreement)")
    parser.add_argument("--out", default=None, help="Directory to write a JSON report into")
    args = parser.parse_args()

    fixtures = args.fixtures or sorted(glob.glob(os.path.join(os.path.dirname(__file__), "golden", "*.json")))
    if not fixtures:
        print("No fixtures found under eval/golden/.", file=sys.stderr)
        sys.exit(1)

    acc = asyncio.run(_run(fixtures, max(1, args.samples)))
    report = acc.report()
    report["generated_at"] = datetime.now(timezone.utc).isoformat()
    report["fixtures"] = fixtures
    report["samples"] = max(1, args.samples)

    print(json.dumps(report, indent=2, ensure_ascii=False))

    if args.out:
        os.makedirs(args.out, exist_ok=True)
        stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
        out_path = os.path.join(args.out, f"golden_report_{stamp}.json")
        with open(out_path, "w", encoding="utf-8") as fh:
            json.dump(report, fh, indent=2, ensure_ascii=False)
        print(f"\nReport written to {out_path}", file=sys.stderr)


if __name__ == "__main__":
    main()
