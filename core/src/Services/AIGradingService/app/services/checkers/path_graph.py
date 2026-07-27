"""Path-graph checker for network-diagram style questions (PMG201c R4).

Answer key format (in contract answerKey):
  CHECKER:path_graph
  PATHS: ABCG=15; DBCG=16; DEFG=17; DHI=19

When the student answer does not contain the required path tokens with correct totals,
force path-related check-items to "no".
"""

from __future__ import annotations

import re

from app.schemas.grading import ScoreGridItem

_PATH_DASHED_RE = re.compile(
    r"([A-Z](?:[-–>→][A-Z])+)\s*=\s*(\d+)",
    re.IGNORECASE,
)
_PATH_COMPACT_RE = re.compile(
    r"\b([A-Z]{2,})\s*=\s*(\d+)\b",
    re.IGNORECASE,
)


def _compact_alpha(text: str) -> str:
    return re.sub(r"[^A-Z]", "", text.upper())


def _parse_required_paths(answer_key: str) -> list[tuple[str, int]]:
    paths: list[tuple[str, int]] = []
    for line in answer_key.splitlines():
        line = line.strip()
        if not line.upper().startswith("PATHS:"):
            continue
        segment = line.split(":", 1)[1]
        for part in re.split(r"[;,\n]", segment):
            part = part.strip()
            if not part:
                continue
            m = _PATH_DASHED_RE.search(part)
            if m:
                paths.append((_compact_alpha(m.group(1)), int(m.group(2))))
                continue
            m2 = _PATH_COMPACT_RE.search(part)
            if m2:
                paths.append((_compact_alpha(m2.group(1)), int(m2.group(2))))
    return paths


def _student_has_path(answer_text: str, compact_path: str, total: int) -> bool:
    """True if the answer mentions the path (compact letters) with the expected total."""
    if str(total) not in answer_text:
        return False
    return compact_path in _compact_alpha(answer_text)


def apply_path_graph_checker(
    item: ScoreGridItem,
    answer_text: str,
    verdict_by_check: dict[str, str],
) -> dict[str, str]:
    required = _parse_required_paths(item.answer_key or "")
    if not required:
        return verdict_by_check

    matched = sum(
        1 for path, total in required if _student_has_path(answer_text, path, total)
    )
    ratio = matched / len(required)

    updated = dict(verdict_by_check)
    for check in item.check_items:
        desc = check.description.lower()
        if any(kw in desc for kw in ("path", "đường", "route", "lộ trình")):
            if ratio >= 1.0:
                updated[check.check_id] = "yes"
            elif ratio > 0:
                updated[check.check_id] = "partial"
            else:
                updated[check.check_id] = "no"
    return updated
