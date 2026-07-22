"""Adjudicator (doc 5.4 — step B5).

Resolves the N independent verdict samples for a criterion into one verdict per check-item by
majority vote, and measures inter-sample agreement. Check-items with no clear majority are marked
"unclear"; low overall agreement flags the criterion for MANUAL review.
"""

from __future__ import annotations

from collections import Counter
from dataclasses import dataclass, field
from statistics import mean

from app.schemas.grading import CriterionVerdict

# Below this mean inter-sample agreement the criterion is too uncertain to auto-accept.
_MANUAL_AGREEMENT_FLOOR = 0.5


@dataclass
class Adjudication:
    verdict_by_check: dict[str, str] = field(default_factory=dict)
    evidence_by_check: dict[str, list[str]] = field(default_factory=dict)
    agreement: float = 1.0
    needs_manual: bool = False


def adjudicate(samples: list[CriterionVerdict], check_ids: list[str]) -> Adjudication:
    """Majority-vote verdicts across samples; collect evidence from the winning votes."""
    if not samples:
        return Adjudication(
            verdict_by_check={cid: "unclear" for cid in check_ids},
            agreement=0.0,
            needs_manual=True,
        )

    verdict_by_check: dict[str, str] = {}
    evidence_by_check: dict[str, list[str]] = {}
    agreements: list[float] = []

    for cid in check_ids:
        votes: list[str] = []
        evidences: list[tuple[str, list[str]]] = []
        for sample in samples:
            match = next((v for v in sample.verdicts if v.check_id == cid), None)
            verdict = match.verdict if match else "unclear"
            votes.append(verdict)
            if match:
                evidences.append((verdict, match.evidence))

        counts = Counter(votes)
        top_verdict, top_count = counts.most_common(1)[0]
        verdict_by_check[cid] = top_verdict
        agreements.append(top_count / len(votes))

        # Evidence from the samples that agreed with the winning verdict.
        merged: list[str] = []
        for verdict, evs in evidences:
            if verdict == top_verdict:
                merged.extend(evs)
        # de-dup preserving order
        evidence_by_check[cid] = list(dict.fromkeys(e for e in merged if e.strip()))

    overall = mean(agreements) if agreements else 1.0
    return Adjudication(
        verdict_by_check=verdict_by_check,
        evidence_by_check=evidence_by_check,
        agreement=overall,
        needs_manual=overall < _MANUAL_AGREEMENT_FLOOR,
    )
