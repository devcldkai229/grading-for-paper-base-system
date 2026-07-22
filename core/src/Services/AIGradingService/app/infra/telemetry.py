"""OpenTelemetry wiring for the AI grading worker (Phase 0).

Gives the Python service the same trace backbone as the .NET services so a single request can be
followed end-to-end — including the hop through RabbitMQ. HTTP paths (FastAPI, httpx file downloads)
are auto-instrumented; the RabbitMQ path is propagated manually because the messages are wrapped in a
custom MassTransit envelope (see masstransit_envelope.py).

If ``OTEL_EXPORTER_OTLP_ENDPOINT`` is not set the tracer provider is still installed but without an
exporter, so spans are created (and context propagates across the wire) without requiring a collector
— keeping local runs and tests dependency-free.
"""

from __future__ import annotations

import logging
import os

from opentelemetry import metrics, trace
from opentelemetry.propagate import extract, inject
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import SERVICE_NAME, Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor

logger = logging.getLogger(__name__)

SERVICE_NAME_VALUE = "ai-grading-service"

_configured = False


def configure_telemetry(app: object | None = None) -> None:
    """Install the global tracer provider and instrument FastAPI + httpx. Idempotent."""
    global _configured
    if _configured:
        return

    # MassTransit conversationId is not valid W3C baggage; keep propagation to tracecontext only
    # so Python does not spam "Invalid baggage entry: messaging.message.conversation_id=...".
    os.environ["OTEL_PROPAGATORS"] = "tracecontext"
    try:
        from opentelemetry.propagate import set_global_textmap
        from opentelemetry.trace.propagation.tracecontext import TraceContextTextMapPropagator

        set_global_textmap(TraceContextTextMapPropagator())
    except Exception:  # noqa: BLE001
        logger.exception("Failed to pin OTEL propagator to tracecontext")

    resource = Resource.create({SERVICE_NAME: SERVICE_NAME_VALUE})
    provider = TracerProvider(resource=resource)

    endpoint = os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT")
    if endpoint:
        try:
            from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter

            provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter()))
            logger.info("OTel OTLP exporter enabled (endpoint=%s)", endpoint)
        except Exception:  # noqa: BLE001
            logger.exception("Failed to initialise OTLP exporter — running without span export")
    else:
        logger.info("OTEL_EXPORTER_OTLP_ENDPOINT not set — spans created but not exported")

    trace.set_tracer_provider(provider)

    # Metrics provider for business counters (§9.1b). Without an OTLP endpoint we still install a
    # provider (counters become no-ops) so instrument code paths never branch on configuration.
    metric_readers = []
    if endpoint:
        try:
            from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter

            metric_readers.append(PeriodicExportingMetricReader(OTLPMetricExporter()))
        except Exception:  # noqa: BLE001
            logger.exception("Failed to initialise OTLP metric exporter — running without metric export")
    metrics.set_meter_provider(MeterProvider(resource=resource, metric_readers=metric_readers))

    try:
        from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor

        HTTPXClientInstrumentor().instrument()
    except Exception:  # noqa: BLE001
        logger.exception("Failed to instrument httpx")

    if app is not None:
        try:
            from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

            FastAPIInstrumentor.instrument_app(app)
        except Exception:  # noqa: BLE001
            logger.exception("Failed to instrument FastAPI app")

    _configured = True


def get_tracer() -> trace.Tracer:
    """Return the service tracer (a no-op tracer if telemetry was never configured)."""
    return trace.get_tracer(SERVICE_NAME_VALUE)


def extract_context(carrier: dict[str, str]):
    """Rebuild the remote trace context from W3C headers carried on the message."""
    return extract(carrier)


def inject_context(carrier: dict[str, str]) -> dict[str, str]:
    """Write the current trace context (traceparent/tracestate) into a carrier for outbound messages."""
    inject(carrier)
    return carrier
