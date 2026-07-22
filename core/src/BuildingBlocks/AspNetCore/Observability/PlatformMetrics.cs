using System.Diagnostics.Metrics;

namespace BuildingBlocks.AspNetCore.Observability;

/// <summary>
/// Shared business-metrics <see cref="Meter"/> (§9.1b). Instruments created off this meter are picked
/// up by OpenTelemetry because <see cref="Extensions.PlatformObservabilityExtensions"/> registers
/// <see cref="MeterName"/> with the metrics pipeline. Use this for domain KPIs that are not covered by
/// the built-in AspNetCore/HttpClient/runtime instrumentation.
/// </summary>
public static class PlatformMetrics
{
    public const string MeterName = "GradingSystem.Business";

    public static readonly Meter Business = new(MeterName);
}
