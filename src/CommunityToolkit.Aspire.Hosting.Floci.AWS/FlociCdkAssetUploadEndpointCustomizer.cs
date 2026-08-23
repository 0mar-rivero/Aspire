using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Internal;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Internal;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS;

/// <summary>Redirects AWS SDK clients created by the CDK asset uploader to Floci.</summary>
internal sealed class FlociCdkAssetUploadEndpointCustomizer(Uri flociUrl) : IRuntimePipelineCustomizer
{
    private const string RegistrationName = "CommunityToolkit.Aspire.Hosting.Floci.AWS.CdkAssetUpload";

    public string UniqueName => RegistrationName;

    /// <summary>
    /// Registers a customizer for the supplied Floci endpoint, replacing the previous registration.
    /// </summary>
    /// <remarks>
    /// The AWS SDK registry is process-wide, so registration is last-writer-wins. Overlapping CDK
    /// deployments targeting distinct Floci endpoints are not a supported topology.
    /// </remarks>
    internal static void Register(Uri flociUrl)
    {
        ArgumentNullException.ThrowIfNull(flociUrl);
        RuntimePipelineCustomizerRegistry.Instance.Deregister(RegistrationName);
        RuntimePipelineCustomizerRegistry.Instance.Register(new FlociCdkAssetUploadEndpointCustomizer(flociUrl));
    }

    internal static void Deregister() => RuntimePipelineCustomizerRegistry.Instance.Deregister(RegistrationName);

    public void Customize(Type type, RuntimePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(pipeline);

        // These insertion points intentionally depend on the AWS SDK endpoint resolver types and
        // ordering. Focused tests pin their positions so an SDK change fails during validation.
        if (type == typeof(AmazonS3Client))
        {
            if (!HasHandler<FlociCdkAssetUploadForcePathStyleHandler>(pipeline))
            {
                pipeline.AddHandlerBefore<AmazonS3EndpointResolver>(new FlociCdkAssetUploadForcePathStyleHandler());
            }

            if (!HasHandler<FlociCdkAssetUploadEndpointRedirectHandler>(pipeline))
            {
                pipeline.AddHandlerAfter<AmazonS3EndpointResolver>(new FlociCdkAssetUploadEndpointRedirectHandler(flociUrl));
            }
        }
        else if (type == typeof(AmazonSecurityTokenServiceClient) &&
                 !HasHandler<FlociCdkAssetUploadEndpointRedirectHandler>(pipeline))
        {
            pipeline.AddHandlerAfter<AmazonSecurityTokenServiceEndpointResolver>(new FlociCdkAssetUploadEndpointRedirectHandler(flociUrl));
        }
    }

    private static bool HasHandler<THandler>(RuntimePipeline pipeline)
        where THandler : IPipelineHandler
        => pipeline.EnumerateHandlers().Any(static handler => handler is THandler);
}

/// <summary>Forces path-style addressing on S3 clients used to upload CDK assets.</summary>
internal sealed class FlociCdkAssetUploadForcePathStyleHandler : PipelineHandler
{
    public override void InvokeSync(IExecutionContext executionContext)
    {
        EnableForcePathStyle(executionContext);
        base.InvokeSync(executionContext);
    }

    public override Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        EnableForcePathStyle(executionContext);
        return base.InvokeAsync<T>(executionContext);
    }

    private static void EnableForcePathStyle(IExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        if (executionContext.RequestContext.ClientConfig is AmazonS3Config config)
        {
            config.ForcePathStyle = true;
        }
    }
}

/// <summary>Replaces the authority of an AWS SDK request endpoint with the Floci endpoint.</summary>
internal sealed class FlociCdkAssetUploadEndpointRedirectHandler(Uri flociUrl) : PipelineHandler
{
    public override void InvokeSync(IExecutionContext executionContext)
    {
        RedirectEndpoint(executionContext);
        base.InvokeSync(executionContext);
    }

    public override Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        RedirectEndpoint(executionContext);
        return base.InvokeAsync<T>(executionContext);
    }

    private void RedirectEndpoint(IExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        if (executionContext.RequestContext.Request.Endpoint is not { } resolved)
        {
            return;
        }

        executionContext.RequestContext.Request.Endpoint = new UriBuilder(resolved)
        {
            Scheme = flociUrl.Scheme,
            Host = flociUrl.Host,
            Port = flociUrl.Port
        }.Uri;
    }
}
