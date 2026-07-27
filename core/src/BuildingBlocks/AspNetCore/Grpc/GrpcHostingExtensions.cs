using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.AspNetCore.Grpc;

/// <summary>
/// Hosting helper for services that expose both a REST/HTTP surface and a gRPC surface. gRPC needs
/// HTTP/2, and mixing HTTP/1.1 + HTTP/2 on a single cleartext (h2c) port is unreliable, so this
/// binds a dedicated Http2-only port for gRPC alongside the existing HTTP port.
/// <para>
/// Note: calling Kestrel's <c>Listen</c> API disables the URL-based configuration
/// (<c>ASPNETCORE_URLS</c>/<c>ASPNETCORE_HTTP_PORTS</c>/launch-profile <c>applicationUrl</c>). To
/// avoid dropping the existing HTTP endpoint, the HTTP port is resolved from that same configuration
/// (or an explicit <c>Http:Port</c>) and re-bound here; the gRPC port comes from <c>Grpc:Port</c>.
/// </para>
/// </summary>
public static class GrpcHostingExtensions
{
    public static void AddDualProtocolGrpcHosting(
        this WebApplicationBuilder builder, int defaultHttpPort, int defaultGrpcPort)
    {
        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            var httpPort = ResolveHttpPort(context.Configuration, defaultHttpPort);
            var grpcPort = context.Configuration.GetValue<int?>("Grpc:Port") ?? defaultGrpcPort;

            options.ListenAnyIP(httpPort, o => o.Protocols = HttpProtocols.Http1AndHttp2);
            options.ListenAnyIP(grpcPort, o => o.Protocols = HttpProtocols.Http2);
        });
    }

    private static int ResolveHttpPort(IConfiguration configuration, int defaultHttpPort)
    {
        if (configuration.GetValue<int?>("Http:Port") is { } explicitPort)
        {
            return explicitPort;
        }

        // launch-profile / ASPNETCORE_URLS lands under the "urls" host-configuration key.
        foreach (var key in new[] { "urls", "ASPNETCORE_URLS" })
        {
            if (!string.IsNullOrWhiteSpace(configuration[key])
                && ParseFirstHttpPort(configuration[key]!) is { } urlPort)
            {
                return urlPort;
            }
        }

        // ASPNETCORE_HTTP_PORTS (the .NET container default, e.g. 8080) lands under "http_ports".
        foreach (var key in new[] { "http_ports", "ASPNETCORE_HTTP_PORTS" })
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                var first = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
                if (int.TryParse(first, out var httpPortsPort))
                {
                    return httpPortsPort;
                }
            }
        }

        return defaultHttpPort;
    }

    private static int? ParseFirstHttpPort(string urls)
    {
        foreach (var raw in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = raw.Replace("*", "localhost").Replace("+", "localhost");
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp)
            {
                return uri.Port;
            }
        }
        return null;
    }
}
