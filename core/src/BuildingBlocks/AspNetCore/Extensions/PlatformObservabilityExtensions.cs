using System.Diagnostics;
using BuildingBlocks.AspNetCore.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace BuildingBlocks.AspNetCore.Extensions;

/// <summary>
/// One-call wiring for the platform observability baseline (Phase 0): structured JSON logging with
/// trace correlation, OpenTelemetry traces + metrics exported over OTLP, and resilient HttpClients.
/// Traces flow across process boundaries automatically: AspNetCore/HttpClient instrumentation carry
/// W3C <c>traceparent</c> over HTTP, and MassTransit propagates it over AMQP — so a single request can
/// be followed end-to-end, including the hop through RabbitMQ into the Python AI worker.
/// </summary>
public static class PlatformObservabilityExtensions
{
    /// <summary>ActivitySources emitted by dependencies we want captured as spans.</summary>
    private static readonly string[] TracedSources = ["MassTransit", "Npgsql"];

    public static WebApplicationBuilder AddPlatformObservability(
        this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // Structured JSON logs to stdout, correlated with the active trace/span (N4).
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((_, cfg) => cfg
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .Enrich.With(new ActivityTraceEnricher())
            .WriteTo.Console(new RenderedCompactJsonFormatter()));

        var resource = ResourceBuilder.CreateDefault().AddService(serviceName);

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resource)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(TracedSources);

                if (HasOtlpEndpoint())
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resource)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(Observability.PlatformMetrics.MeterName);

                if (HasOtlpEndpoint())
                {
                    metrics.AddOtlpExporter();
                }
            });

        // Timeout + retry + circuit-breaker + bulkhead on every HttpClient in the app (N6),
        // without touching individual AddHttpClient registrations.
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        return builder;
    }

    /// <summary>
    /// Maps the three-tier health probe convention: <c>/health/live</c> (process up),
    /// <c>/health/startup</c> (dependencies reachable during boot), <c>/health/ready</c> (accept traffic).
    /// Both startup and ready use checks tagged <c>ready</c>.
    /// </summary>
    public static WebApplication MapPlatformHealthChecks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = HealthCheckResponseWriter.WriteMinimalJson
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = HealthCheckResponseWriter.WriteMinimalJson
        });

        return app;
    }

    private static bool HasOtlpEndpoint() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

    /// <summary>Copies the current trace/span ids onto every log event so logs join traces in Grafana/Loki.</summary>
    private sealed class ActivityTraceEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
        {
            var activity = Activity.Current;
            if (activity is null)
            {
                return;
            }

            logEvent.AddPropertyIfAbsent(factory.CreateProperty("traceId", activity.TraceId.ToString()));
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("spanId", activity.SpanId.ToString()));
        }
    }
}
