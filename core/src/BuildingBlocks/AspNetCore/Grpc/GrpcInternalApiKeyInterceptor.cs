using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Grpc;

/// <summary>
/// Server-side gRPC interceptor that enforces the shared internal API key. It plays the same role
/// for gRPC calls that <c>InternalApiKeyMiddleware</c> plays for the REST <c>/api/internal</c> paths
/// (that middleware is path-scoped and never sees gRPC requests). Reads the expected key from
/// configuration key <c>InternalAuth:ApiKey</c> and compares it against the <c>x-internal-api-key</c>
/// request metadata; a missing/mismatched key fails the call with <see cref="StatusCode.Unauthenticated"/>.
/// </summary>
public sealed class GrpcInternalApiKeyInterceptor : Interceptor
{
    public const string MetadataKey = "x-internal-api-key";

    private readonly string? _apiKey;
    private readonly ILogger<GrpcInternalApiKeyInterceptor> _logger;

    public GrpcInternalApiKeyInterceptor(
        IConfiguration configuration,
        ILogger<GrpcInternalApiKeyInterceptor> logger)
    {
        _apiKey = configuration["InternalAuth:ApiKey"];
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(requestStream, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        await continuation(request, responseStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        await continuation(requestStream, responseStream, context);
    }

    private void Authenticate(ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("Internal gRPC authentication is not configured (InternalAuth:ApiKey missing).");
            throw new RpcException(new Status(
                StatusCode.Unauthenticated, "Internal authentication is not configured."));
        }

        var provided = context.RequestHeaders.GetValue(MetadataKey);
        if (string.IsNullOrEmpty(provided) || !CryptographicEquals(provided, _apiKey))
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated, "Invalid or missing internal API key."));
        }
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
