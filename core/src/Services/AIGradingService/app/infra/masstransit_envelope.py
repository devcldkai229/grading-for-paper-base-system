"""MassTransit interop — parse/build the JSON envelope MassTransit puts on RabbitMQ.

MassTransit (the .NET side) wraps every message in an envelope:

    {
      "messageId": "<guid>",
      "messageType": ["urn:message:Contracts.Messages:AiGradeRequestedEvent"],
      "message": { ...the actual payload, camelCase... },
      "sentTime": "2026-07-21T...Z"
    }

and publishes it to a fanout exchange named after the message type
(``Contracts.Messages:AiGradeRequestedEvent``) with AMQP content-type
``application/vnd.masstransit+json``.

This module lets the Python worker speak that same protocol so it can consume
``AiGradeRequestedEvent`` and publish ``AiGradeCompletedEvent`` /
``AiGradeFailedEvent`` that the .NET consumers deserialize natively.
"""

from __future__ import annotations

import uuid
from datetime import datetime, timezone
from typing import Any

CONTENT_TYPE = "application/vnd.masstransit+json"

CONTRACTS_NAMESPACE = "Contracts.Messages"

# W3C trace-context header names. MassTransit places these in the envelope ``headers`` object and on the
# AMQP transport headers when distributed tracing is active; we mirror both so traces stitch together.
_TRACE_HEADER_NAMES = ("traceparent", "tracestate")

# Stable namespace for deriving deterministic reply message ids (so redelivery of the same
# request yields the same completed/failed MessageId → MassTransit inbox dedupe is exact).
_REPLY_ID_NAMESPACE = uuid.UUID("6f9619ff-8b86-d011-b42d-00c04fc964ff")


def message_type_urn(name: str) -> str:
    """Full MassTransit message-type URN for a Contracts.Messages record."""
    return f"urn:message:{CONTRACTS_NAMESPACE}:{name}"


def exchange_name(name: str) -> str:
    """The fanout exchange MassTransit uses for a Contracts.Messages record."""
    return f"{CONTRACTS_NAMESPACE}:{name}"


def deterministic_reply_id(assignment_id: str, request_message_id: str | None) -> str:
    """Derive a stable MessageId for a reply so redelivery is idempotent."""
    seed = f"{assignment_id}:{request_message_id or ''}"
    return str(uuid.uuid5(_REPLY_ID_NAMESPACE, seed))


def parse_envelope(raw_body: bytes) -> dict[str, Any]:
    """Parse the full MassTransit envelope from a raw message body."""
    import json

    envelope = json.loads(raw_body)
    return envelope if isinstance(envelope, dict) else {}


def message_of(envelope: dict[str, Any]) -> dict[str, Any]:
    """Extract the inner ``message`` payload from a parsed envelope (defensive to raw JSON)."""
    if "message" in envelope:
        message = envelope.get("message")
        return message if isinstance(message, dict) else {}
    return envelope


def parse_message(raw_body: bytes) -> dict[str, Any]:
    """Extract the inner ``message`` object from a MassTransit envelope body."""
    return message_of(parse_envelope(raw_body))


def extract_trace_carrier(
    envelope: dict[str, Any], amqp_headers: dict[str, Any] | None
) -> dict[str, str]:
    """Collect W3C trace headers from the envelope headers and/or AMQP transport headers."""
    carrier: dict[str, str] = {}
    envelope_headers = envelope.get("headers") if isinstance(envelope, dict) else None
    for source in (envelope_headers, amqp_headers):
        if not isinstance(source, dict):
            continue
        for name in _TRACE_HEADER_NAMES:
            value = ci_get(source, name)
            if value is not None and name not in carrier:
                carrier[name] = str(value)
    return carrier


def build_envelope(
    message_type_name: str,
    message: dict[str, Any],
    message_id: str,
    headers: dict[str, str] | None = None,
) -> dict[str, Any]:
    """Wrap a payload in a MassTransit-compatible envelope ready to serialize to JSON.

    ``headers`` (e.g. an injected ``traceparent``) is embedded so downstream MassTransit consumers
    continue the same distributed trace.
    """
    return {
        "messageId": message_id,
        "conversationId": str(uuid.uuid4()),
        "messageType": [message_type_urn(message_type_name)],
        "message": message,
        "headers": headers or {},
        "sentTime": datetime.now(timezone.utc).isoformat(),
    }


def ci_get(source: dict[str, Any], *names: str, default: Any = None) -> Any:
    """Case-insensitive lookup — tolerates camelCase or PascalCase envelope keys."""
    if not isinstance(source, dict):
        return default
    lowered = {str(k).lower(): v for k, v in source.items()}
    for name in names:
        value = lowered.get(name.lower())
        if value is not None:
            return value
    return default
