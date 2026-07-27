import io
import json
import re
from typing import Any

import httpx
from docx import Document
from openai import OpenAI
from pypdf import PdfReader

from app.config import settings
from app.schemas.rubric import ExtractedQuestion, ExtractRubricResponse


async def download_file(url: str) -> bytes:
    async with httpx.AsyncClient(timeout=60) as client:
        resp = await client.get(url)
        resp.raise_for_status()
        return resp.content


def extract_text_pdf(data: bytes) -> str:
    reader = PdfReader(io.BytesIO(data))
    parts: list[str] = []
    for page in reader.pages:
        text = page.extract_text() or ""
        if text.strip():
            parts.append(text)
    return "\n\n".join(parts).strip()


def extract_text_docx(data: bytes) -> str:
    doc = Document(io.BytesIO(data))
    return "\n".join(p.text for p in doc.paragraphs if p.text.strip())


def normalize_content(data: bytes, content_type: str) -> str:
    ct = content_type.lower()
    if "pdf" in ct:
        text = extract_text_pdf(data)
        if text:
            return text
        return "[PDF scan/image — vision model required]"
    if "wordprocessingml" in ct or ct.endswith("docx"):
        return extract_text_docx(data)
    if ct.startswith("text/"):
        return data.decode("utf-8", errors="replace")
    if ct.startswith("image/"):
        return "[IMAGE_CONTENT]"
    raise ValueError(f"Unsupported content type: {content_type}")


SYSTEM_PROMPT = """You read exam rubrics (barem) and output a grading SCORE GRID as JSON only:
{"questions":[{"groupLabel":string|null,"questionNumber":string,"label":string,"maxScore":number,"confidence":number}]}

The score grid is the list of QUESTIONS to grade — NOT the fine-grained marking bullets inside a grading guide.

Rubric styles you will see:
- Flat point lists (Câu 1: 2đ, Câu 2: 3đ)
- Percentage sections (Request 1 (20%), Yêu cầu 2 (30%)) — most common for project exams
- Mixed Vietnamese/English

CRITICAL — do NOT over-split:
- When a section is "Request N (X%)" / "Yêu cầu N (X%)" / "Câu N (X%)" with ONE section total
  (e.g. 20% of 10 = 2 points), output EXACTLY ONE row for that section.
  questionNumber = "N", maxScore = section budget in points, groupLabel = full section title.
- Do NOT turn grading-guide bullets into separate rows. Phrases like
  "mỗi mô tả đúng được 0.5đ", "gọi đúng tên 0.3đ, lý do thêm 0.7đ", "mỗi rủi ro 0.3đ"
  are PARTIAL-CREDIT RULES for later contract compilation — they are NOT score-grid leaves.
- Only split into sub-leaves (1.1, 1.2, …) when the rubric itself lists DISTINCT numbered
  sub-questions with their OWN explicit totals (e.g. "1a (1đ)", "1b (1đ)"), not when it
  only describes how to award marks inside one request.

Output shape:
- Each row: one scorable question leaf, maxScore > 0.
- groupLabel: section title on every row in that section (e.g. "Request 1 (20%)"); null only if no grouping.
- questionNumber: stable id ("1", "2", "3"), unique.
- label: short criterion summary from the request stem (under ~120 characters), not the whole guide.
- confidence: 0–1.

Scoring:
- maxScore is always in POINTS (never raw percentages).
- When subjectMaxScore is given, all leaves MUST sum exactly to it.
- Convert (X%) headers to points: sectionBudget = subjectMaxScore * X / 100.
- Prefer clean decimals (0.5, 1, 2) — avoid long floats like 0.43 or 1.999999.
"""

MAX_GROUP_LABEL_LEN = 100
MAX_LABEL_LEN = 255
MAX_QUESTION_NUMBER_LEN = 20


def _truncate(text: str | None, max_len: int) -> str | None:
    if not text:
        return text
    trimmed = text.strip()
    if len(trimmed) <= max_len:
        return trimmed
    return trimmed[:max_len].rstrip()


def sanitize_question_fields(
    questions: list[ExtractedQuestion],
) -> tuple[list[ExtractedQuestion], list[str]]:
    warnings: list[str] = []
    for q in questions:
        if len(q.question_number) > MAX_QUESTION_NUMBER_LEN:
            old = q.question_number
            q.question_number = _truncate(q.question_number, MAX_QUESTION_NUMBER_LEN) or old[:MAX_QUESTION_NUMBER_LEN]
            warnings.append(f"{old}: questionNumber truncated to {MAX_QUESTION_NUMBER_LEN} chars")

        if q.group_label and len(q.group_label) > MAX_GROUP_LABEL_LEN:
            old_len = len(q.group_label)
            q.group_label = _truncate(q.group_label, MAX_GROUP_LABEL_LEN)
            warnings.append(f"{q.question_number}: groupLabel truncated ({old_len} -> {MAX_GROUP_LABEL_LEN})")

        if q.label and len(q.label) > MAX_LABEL_LEN:
            old_len = len(q.label)
            q.label = _truncate(q.label, MAX_LABEL_LEN) or ""
            warnings.append(f"{q.question_number}: label truncated ({old_len} -> {MAX_LABEL_LEN})")

    return questions, warnings


def _parent_key(question_number: str) -> str | None:
    qn = question_number.strip()
    if not qn:
        return None
    for sep in (".", "-", "_"):
        if sep in qn:
            return qn.split(sep, 1)[0].strip()
    return qn


def parse_section_headers(text: str) -> list[tuple[str, str]]:
    """Section number -> header label, in document order."""
    if not text or text.startswith("["):
        return []

    pattern = re.compile(
        r"(?im)^\s*((?:Request|Yêu cầu|Yeucau|Câu|Part)\s+(\d+)(?:\s*\([^)]+\))?)"
    )
    seen: set[str] = set()
    ordered: list[tuple[str, str]] = []
    for match in pattern.finditer(text):
        num = match.group(2)
        if num in seen:
            continue
        seen.add(num)
        ordered.append((num, match.group(1).strip()))
    return ordered


def infer_missing_group_labels(
    questions: list[ExtractedQuestion], rubric_text: str
) -> tuple[list[ExtractedQuestion], list[str]]:
    """Fill groupLabel when LLM omitted it but numbering or rubric text implies a section."""
    if not questions:
        return questions, []

    from collections import defaultdict

    warnings: list[str] = []
    headers = parse_section_headers(rubric_text)
    header_map = dict(headers)

    by_parent: dict[str, list[ExtractedQuestion]] = defaultdict(list)
    for q in questions:
        pk = _parent_key(q.question_number)
        if pk:
            by_parent[pk].append(q)

    for parent, items in by_parent.items():
        existing = next(
            (i.group_label.strip() for i in items if i.group_label and i.group_label.strip()),
            None,
        )
        if existing:
            for item in items:
                if not (item.group_label or "").strip():
                    item.group_label = existing
                    warnings.append(
                        f"{item.question_number}: groupLabel set from sibling section ({existing})"
                    )
            continue

        if parent in header_map:
            label = header_map[parent]
            for item in items:
                if not (item.group_label or "").strip():
                    item.group_label = label
                    warnings.append(
                        f"{item.question_number}: groupLabel from rubric section ({label})"
                    )

    still_ungrouped = [
        q
        for q in questions
        if not (q.group_label or "").strip()
        and _parent_key(q.question_number) == q.question_number.strip()
    ]
    if headers and len(still_ungrouped) == len(headers):
        for q, (_, label) in zip(
            sorted(still_ungrouped, key=lambda x: x.question_number), headers
        ):
            q.group_label = label
            warnings.append(f"{q.question_number}: groupLabel mapped to section ({label})")

    missing = sum(1 for q in questions if not (q.group_label or "").strip())
    if missing and headers:
        warnings.append(f"{missing} row(s) still have no groupLabel")

    return questions, warnings


def parse_percent_from_text(text: str | None) -> float | None:
    if not text:
        return None
    match = re.search(r"\((\d+(?:\.\d+)?)\s*%\)", text, re.IGNORECASE)
    if match:
        return float(match.group(1))
    match = re.search(r"(\d+(?:\.\d+)?)\s*%\s*[:)]", text, re.IGNORECASE)
    if match:
        return float(match.group(1))
    return None


def _round2(value: float) -> float:
    """Banker's-avoiding 2dp round for score display (0.5 → 0.5, no 1.9999998)."""
    return round(float(value) + 1e-9, 2)


def _section_number_from_group(group_label: str, items: list[ExtractedQuestion]) -> str:
    """Prefer 'Request 3 (20%)' → '3'; else parent of first questionNumber."""
    match = re.search(
        r"(?:Request|Yêu cầu|Yeucau|Câu|Part)\s+(\d+)", group_label, re.IGNORECASE
    )
    if match:
        return match.group(1)
    for item in items:
        pk = _parent_key(item.question_number)
        if pk:
            return pk
    return items[0].question_number.strip() if items else "1"


def collapse_percentage_sections(
    questions: list[ExtractedQuestion], subject_max_score: float | None
) -> tuple[list[ExtractedQuestion], list[str]]:
    """Collapse over-split % sections into one leaf per Request/Yêu cầu.

    LLM often turns grading-guide bullets (0.3đ + 0.7đ …) into many score-grid rows.
    Those belong in the compiled contract as checkItems, not as separate grid leaves.
    """
    if subject_max_score is None or subject_max_score <= 0 or not questions:
        return questions, []

    from collections import defaultdict

    warnings: list[str] = []
    by_group: dict[str, list[ExtractedQuestion]] = defaultdict(list)
    order: list[str] = []
    for q in questions:
        key = (q.group_label or "").strip() or "_ungrouped"
        if key not in by_group:
            order.append(key)
        by_group[key].append(q)

    collapsed: list[ExtractedQuestion] = []
    for group_label in order:
        items = by_group[group_label]
        pct = parse_percent_from_text(group_label)
        if pct is None:
            for item in items:
                pct = parse_percent_from_text(item.label)
                if pct is not None:
                    break

        if pct is None:
            collapsed.extend(items)
            continue

        expected = _round2(subject_max_score * pct / 100.0)
        qn = _section_number_from_group(group_label, items)

        if len(items) == 1:
            item = items[0]
            if item.question_number.strip() != qn:
                warnings.append(
                    f"{group_label}: questionNumber {item.question_number} -> {qn}"
                )
                item.question_number = qn
            if abs(_round2(item.max_score) - expected) > 0.05:
                warnings.append(
                    f"{group_label}: adjusted maxScore {item.max_score} -> {expected}"
                )
                item.max_score = expected
            else:
                item.max_score = expected
            collapsed.append(item)
            continue

        # Prefer the longest non-empty label as the section summary.
        labels = [i.label.strip() for i in items if (i.label or "").strip()]
        label = max(labels, key=len) if labels else group_label
        if len(label) > 120:
            label = label[:117].rstrip() + "..."
        conf = max((i.confidence for i in items), default=0.7)

        collapsed.append(
            ExtractedQuestion(
                groupLabel=group_label if group_label != "_ungrouped" else None,
                questionNumber=qn,
                label=label,
                maxScore=expected,
                confidence=conf,
            )
        )
        warnings.append(
            f"{group_label}: gộp {len(items)} dòng con thành 1 câu "
            f"(maxScore={expected} = {pct}% của {subject_max_score}). "
            f"Các ý chấm chi tiết thuộc hợp đồng AI, không phải lưới điểm."
        )

    return collapsed, warnings


def normalize_percentage_groups(
    questions: list[ExtractedQuestion], subject_max_score: float | None
) -> tuple[list[ExtractedQuestion], list[str]]:
    """Align section totals with (X%) weights when subject max is known."""
    if subject_max_score is None or subject_max_score <= 0 or not questions:
        return questions, []

    from collections import defaultdict

    warnings: list[str] = []
    by_group: dict[str, list[ExtractedQuestion]] = defaultdict(list)
    order: list[str] = []
    for q in questions:
        key = (q.group_label or "").strip() or "_ungrouped"
        if key not in by_group:
            order.append(key)
        by_group[key].append(q)

    normalized: list[ExtractedQuestion] = []

    for group_label in order:
        items = by_group[group_label]
        pct = parse_percent_from_text(group_label)
        if pct is None:
            for item in items:
                pct = parse_percent_from_text(item.label)
                if pct is not None:
                    break

        if pct is None:
            for item in items:
                item.max_score = _round2(item.max_score)
            normalized.extend(items)
            continue

        expected = _round2(subject_max_score * pct / 100.0)
        actual = _round2(sum(i.max_score for i in items))

        if abs(actual - expected) <= 0.05:
            for item in items:
                item.max_score = _round2(item.max_score)
            # Fix residual float drift on last leaf.
            drift = _round2(expected - sum(i.max_score for i in items))
            if abs(drift) >= 0.01 and items:
                items[-1].max_score = _round2(items[-1].max_score + drift)
            normalized.extend(items)
            continue

        if len(items) == 1:
            old = items[0].max_score
            items[0].max_score = expected
            warnings.append(
                f"{group_label}: adjusted maxScore {old} -> {expected} "
                f"({pct}% of {subject_max_score})"
            )
            normalized.extend(items)
            continue

        # Multiple leaves still present (collapse didn't run / no %): scale proportionally.
        if actual > 0:
            factor = expected / actual
            for item in items:
                item.max_score = _round2(item.max_score * factor)
            new_sum = _round2(sum(i.max_score for i in items))
            if abs(new_sum - expected) > 0.01:
                items[-1].max_score = _round2(items[-1].max_score + (expected - new_sum))
            warnings.append(
                f"{group_label}: scaled {len(items)} leaves to sum {expected} "
                f"({pct}% of {subject_max_score}, was {actual})"
            )
        else:
            warnings.append(
                f"{group_label}: expected {expected} pts from {pct}% but leaves sum to 0"
            )

        normalized.extend(items)

    return normalized, warnings


def parse_llm_json(raw: str) -> dict[str, Any]:
    text = raw.strip()
    if text.startswith("```"):
        text = re.sub(r"^```(?:json)?\s*", "", text)
        text = re.sub(r"\s*```$", "", text)
    return json.loads(text)


def validate_questions(
    items: list[dict[str, Any]], subject_max_score: float | None
) -> tuple[list[ExtractedQuestion], list[str]]:
    warnings: list[str] = []
    seen: set[str] = set()
    result: list[ExtractedQuestion] = []

    for i, row in enumerate(items):
        qn = str(row.get("questionNumber", "")).strip()
        if not qn:
            warnings.append(f"Row {i + 1}: missing questionNumber")
            continue
        if qn in seen:
            warnings.append(f"Duplicate questionNumber: {qn}")
            continue
        seen.add(qn)

        try:
            max_score = float(row.get("maxScore", 0))
        except (TypeError, ValueError):
            warnings.append(f"{qn}: invalid maxScore")
            continue
        if max_score <= 0:
            warnings.append(f"{qn}: maxScore must be > 0")
            continue

        confidence = float(row.get("confidence", 0.5))
        if confidence < settings.confidence_threshold:
            warnings.append(f"{qn}: low confidence ({confidence:.2f})")

        result.append(
            ExtractedQuestion(
                groupLabel=(
                    row.get("groupLabel")
                    or row.get("group_label")
                    or row.get("group")
                ),
                questionNumber=qn,
                label=str(row.get("label") or ""),
                maxScore=max_score,
                confidence=confidence,
            )
        )

    total = _round2(sum(q.max_score for q in result))
    if subject_max_score is not None and abs(total - subject_max_score) > 0.01:
        warnings.append(
            f"Total max ({total}) differs from subject max ({subject_max_score})"
        )

    return result, warnings


async def extract_rubric_grid(
    presigned_url: str, content_type: str, subject_max_score: float | None
) -> ExtractRubricResponse:
    if not settings.openai_api_key:
        raise RuntimeError("OPENAI_API_KEY is not configured")

    data = await download_file(presigned_url)
    text_content = normalize_content(data, content_type)

    client = OpenAI(api_key=settings.openai_api_key, timeout=settings.llm_timeout_seconds)

    score_hint = (
        f"\n\nsubjectMaxScore: {subject_max_score}\n"
        "Prefer ONE leaf per Request/Yêu cầu/(X%) section. "
        "Do not emit grading-guide bullet points as separate questions."
        if subject_max_score is not None
        else ""
    )

    user_text = f"Extract the score grid from this rubric.{score_hint}\n\n{text_content}"
    user_parts: list[dict[str, Any]] = [{"type": "text", "text": user_text}]

    if text_content == "[IMAGE_CONTENT]":
        import base64

        b64 = base64.b64encode(data).decode("ascii")
        user_parts = [
            {
                "type": "image_url",
                "image_url": {"url": f"data:{content_type};base64,{b64}"},
            },
            {"type": "text", "text": f"Extract the score grid from this rubric image.{score_hint}"},
        ]

    completion = client.chat.completions.create(
        model=settings.openai_model,
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": user_parts},
        ],
        response_format={"type": "json_object"},
        temperature=0.1,
    )

    raw = completion.choices[0].message.content or "{}"
    try:
        payload = parse_llm_json(raw)
    except json.JSONDecodeError as exc:
        # one repair attempt
        repair = client.chat.completions.create(
            model=settings.openai_model,
            messages=[
                {"role": "system", "content": "Fix JSON only. No prose."},
                {"role": "user", "content": raw},
            ],
            response_format={"type": "json_object"},
            temperature=0,
        )
        payload = parse_llm_json(repair.choices[0].message.content or "{}")
        if not payload.get("questions"):
            raise ValueError("LLM returned invalid JSON") from exc

    questions, warnings = validate_questions(
        payload.get("questions", []), subject_max_score
    )
    questions, group_warnings = infer_missing_group_labels(questions, text_content)
    questions, collapse_warnings = collapse_percentage_sections(
        questions, subject_max_score
    )
    questions, pct_warnings = normalize_percentage_groups(questions, subject_max_score)
    questions, length_warnings = sanitize_question_fields(questions)
    for q in questions:
        q.max_score = _round2(q.max_score)
    warnings = (
        warnings + group_warnings + collapse_warnings + pct_warnings + length_warnings
    )
    # Drop stale "Total max differs" if post-process fixed the sum.
    total_max = _round2(sum(q.max_score for q in questions))
    if subject_max_score is not None and abs(total_max - subject_max_score) <= 0.01:
        warnings = [
            w for w in warnings if not w.startswith("Total max (")
        ]

    return ExtractRubricResponse(
        questions=questions, totalMax=total_max, warnings=warnings
    )
