using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Configuration;

namespace GradingService.Infrastructure.Clients.Grpc;

/// <summary>
/// Client-side gRPC interceptor that attaches the shared internal API key metadata
/// (<c>x-internal-api-key</c>) and a default deadline to every outbound call. Most unary reads
/// get a short deadline; lazy <c>GetCompiledRubric</c> (AI ingest) needs minutes; server-streaming
/// (batch papers) gets a longer one. A deadline/headers already set on the call is respected
/// (never overwritten).
/// </summary>
public sealed class GrpcClientAuthInterceptor : Interceptor
{
    public const string MetadataKey = "x-internal-api-key";

    private static readonly TimeSpan UnaryDeadline = TimeSpan.FromSeconds(10);
    /// <summary>
    /// Lazy compile calls AI ingest (docx→pdf→LLM) which routinely exceeds 1 minute.
    /// </summary>
    private static readonly TimeSpan CompiledRubricDeadline = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StreamingDeadline = TimeSpan.FromSeconds(60);

    private readonly string _apiKey;

    public GrpcClientAuthInterceptor(IConfiguration configuration)
    {
        _apiKey = configuration.GetSection("InternalAuth")["ApiKey"]
            ?? throw new InvalidOperationException("InternalAuth:ApiKey is missing.");
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, WithAuth(context, ResolveUnaryDeadline(context.Method)));
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, WithAuth(context, StreamingDeadline));
    }

    private static TimeSpan ResolveUnaryDeadline(IMethod method)
    {
        // FullName looks like: /gradepaper.examcatalog.v1.RubricService/GetCompiledRubric
        if (method.FullName.Contains("GetCompiledRubric", StringComparison.Ordinal))
        {
            return CompiledRubricDeadline;
        }

        return UnaryDeadline;
    }

    private ClientInterceptorContext<TRequest, TResponse> WithAuth<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context, TimeSpan deadline)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers ?? new Metadata();
        if (headers.GetValue(MetadataKey) is null)
        {
            headers.Add(MetadataKey, _apiKey);
        }

        var options = context.Options.WithHeaders(headers);
        if (options.Deadline is null)
        {
            options = options.WithDeadline(DateTime.UtcNow.Add(deadline));
        }

        return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
    }
}
