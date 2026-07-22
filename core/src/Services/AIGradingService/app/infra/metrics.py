"""Business-metric counters for the AI grading worker (§9.1b).

These are created off a proxy meter, so they can be imported at module load time and start recording
as soon as the meter provider is installed by ``telemetry.configure_telemetry``. When no provider is
configured (or no OTLP endpoint is set) the counters are harmless no-ops.
"""

from __future__ import annotations

from opentelemetry import metrics

_meter = metrics.get_meter("ai-grading-service")

papers_received_total = _meter.create_counter(
    "ai_papers_received_total",
    unit="{papers}",
    description="AI grading requests received from the broker.",
)

papers_graded_total = _meter.create_counter(
    "ai_papers_graded_total",
    unit="{papers}",
    description="AI grading requests that completed successfully.",
)

papers_failed_total = _meter.create_counter(
    "ai_papers_failed_total",
    unit="{papers}",
    description="AI grading requests that ended in a terminal failure.",
)

papers_duplicate_skipped_total = _meter.create_counter(
    "ai_papers_duplicate_skipped_total",
    unit="{papers}",
    description="AI grading requests skipped as duplicates by the Redis inbox.",
)
