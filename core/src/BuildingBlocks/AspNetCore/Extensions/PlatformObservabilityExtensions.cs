using System.Diagnostics;
using BuildingBlocks.AspNetCore.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BuildingBlocks.AspNetCore.Extensions;

/// <summary>
/// One-call wiring for the platform observability baseline (Phase 0): structured logging with
/// trace correlation, OpenTelemetry traces + metrics exported over OTLP, and resilient HttpClients.
/// Console output is human-readable (like Python uvicorn), not compact JSON SQL dumps.
/// </summary>
public static class PlatformObservabilityExtensions
{
    /// <summary>ActivitySources emitted by dependencies we want captured as spans.</summary>
    private static readonly string[] TracedSources = ["MassTransit", "Npgsql"];

    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {service} | {Message:lj}{NewLine}{Exception}";

    public static WebApplicationBuilder AddPlatformObservability(
        this WebApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((_, cfg) => cfg
            .ReadFrom.Configuration(builder.Configuration)
            .MinimumLevel.Information()
            // MassTransit outbox/inbox polls EF every few seconds — those SQL dumps drown real errors.
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Transaction", LogEventLevel.Warning)
            .MinimumLevel.Override("MassTransit", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Information)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http.Resilience", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .Enrich.With(new ActivityTraceEnricher())
            .WriteTo.Console(outputTemplate: ConsoleTemplate));

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

        // No global HttpClient resilience (AttemptTimeout/retry/circuit-breaker). Long AI calls
        // (rubric ingest, grading) were aborted at 10s; retries also multiplied LLM cost.
        // Services that need timeouts set HttpClient.Timeout on the named client instead.

        return builder;
    }

    /// <summary>
    /// Prints a plain-text startup banner (service name + listen addresses) so local consoles
    /// match Python uvicorn clarity — which port this process actually bound.
    /// </summary>
    public static WebApplication LogPlatformStartupBanner(
        this WebApplication app, string serviceName, int? grpcPort = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var addresses = app.Services.GetService<IServer>()
                ?.Features.Get<IServerAddressesFeature>()
                ?.Addresses
                ?.ToArray() ?? [];

            var httpLine = addresses.Length > 0
                ? string.Join(", ", addresses)
                : "(addresses not yet advertised)";

            var env = app.Services.GetRequiredService<IHostEnvironment>().EnvironmentName;
            var lines = new List<string>
            {
                "",
                "────────────────────────────────────────────────────────────",
                $"  {serviceName}",
                $"  env     : {env}",
                $"  listening: {httpLine}",
            };
            if (grpcPort is int g)
            {
                lines.Add($"  gRPC    : http://localhost:{g} (h2c)");
            }

            lines.Add("────────────────────────────────────────────────────────────");
            lines.Add("");

            var banner = string.Join(Environment.NewLine, lines);
            // Plain Console so it is readable even if Serilog sinks change later.
            Console.WriteLine(banner);
            Log.Information("{Service} ready — listening on {Addresses}", serviceName, httpLine);
        });

        return app;
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
