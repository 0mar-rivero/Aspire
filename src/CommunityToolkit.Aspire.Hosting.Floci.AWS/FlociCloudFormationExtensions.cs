using Amazon;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using CommunityToolkit.Aspire.Hosting.Floci.AWS;

namespace Aspire.Hosting;

/// <summary>Provides extensions that route AWS CloudFormation and CDK resources through Floci.</summary>
public static class FlociCloudFormationExtensions
{
    /// <summary>Routes an AWS CloudFormation template or imported stack through a Floci AWS resource.</summary>
    /// <typeparam name="T">The CloudFormation resource type.</typeparam>
    /// <param name="builder">The CloudFormation resource builder.</param>
    /// <param name="floci">The Floci AWS resource.</param>
    /// <param name="region">The AWS region configured for Floci.</param>
    /// <returns>The original resource builder.</returns>
    [AspireExportIgnore(Reason = "Aspire.Hosting.AWS CloudFormation and CDK resources are not available in polyglot AppHosts.")]
    public static IResourceBuilder<T> WithReference<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<FlociAwsContainerResource> floci,
        string region = "us-east-1")
        where T : class, ICloudFormationResource
    {
        ArgumentNullException.ThrowIfNull(floci);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        return WithFlociReferenceCore(builder, floci, new FlociAwsOptions { Region = region });
    }

    /// <summary>Routes an AWS CloudFormation template or CDK stack through Floci using custom .NET AWS SDK configuration.</summary>
    /// <typeparam name="T">The CloudFormation resource type.</typeparam>
    /// <param name="builder">The CloudFormation resource builder.</param>
    /// <param name="floci">The Floci AWS resource.</param>
    /// <param name="options">The Floci AWS client options.</param>
    /// <param name="awsConfig">Optional AWS SDK configuration. Its region takes precedence over <paramref name="options"/>.</param>
    /// <returns>The original resource builder.</returns>
    [AspireExportIgnore(Reason = "Aspire.Hosting.AWS CloudFormation and CDK resources are not available in polyglot AppHosts.")]
    public static IResourceBuilder<T> WithReference<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<FlociAwsContainerResource> floci,
        FlociAwsOptions options,
        IAWSSDKConfig? awsConfig = null)
        where T : class, ICloudFormationResource
    {
        ArgumentNullException.ThrowIfNull(floci);
        ArgumentNullException.ThrowIfNull(options);
        var effectiveOptions = new FlociAwsOptions
        {
            Region = awsConfig?.Region?.SystemName ?? options.Region,
            AccessKeyId = options.AccessKeyId,
            SecretAccessKey = options.SecretAccessKey,
            SessionToken = options.SessionToken
        };
        return WithFlociReferenceCore(builder, floci, effectiveOptions);
    }

    private static IResourceBuilder<T> WithFlociReferenceCore<T>(
        IResourceBuilder<T> builder,
        IResourceBuilder<FlociAwsContainerResource> floci,
        FlociAwsOptions options)
        where T : class, ICloudFormationResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WaitFor(floci);
        builder.WithAnnotation(new FlociEnabledAnnotation(floci.Resource));
        floci.OnConnectionStringAvailable(ConfigureResourceAsync);

        if (!floci.Resource.Annotations.OfType<FlociPropagationAnnotation>().Any())
        {
            floci.WithAnnotation(new FlociPropagationAnnotation());
            builder.ApplicationBuilder.OnBeforeStart((_, _) =>
            {
                PropagateFlociReferences(builder.ApplicationBuilder, floci);
                return Task.CompletedTask;
            });
        }

        return builder;

        async Task ConfigureResourceAsync(FlociAwsContainerResource resource, ConnectionStringAvailableEvent _, CancellationToken cancellationToken)
        {
            var endpoint = await resource.ConnectionStringExpression.GetValueAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new DistributedApplicationException($"Floci endpoint for '{resource.Name}' was not allocated.");
            var uri = new Uri(endpoint);

            FlociResourceConfigurator.ConfigureCloudFormation(builder.Resource, uri, options);
            if (builder.Resource is IStackResource stack)
            {
                stack.AWSSDKConfig = new FlociAwsSdkConfig(RegionEndpoint.GetBySystemName(options.Region));
                FlociCdkCredentialsOverride.Apply(options);
                FlociCdkAssetUploadEndpointCustomizer.Register(uri);
            }
        }
    }

    internal static void PropagateFlociReferences(IDistributedApplicationBuilder builder, IResourceBuilder<FlociAwsContainerResource> floci)
    {
        var enabled = new HashSet<IResource>(ReferenceEqualityComparer.Instance);
        foreach (var resource in builder.Resources)
        {
            if (resource.Annotations.OfType<FlociEnabledAnnotation>().Any(annotation => ReferenceEquals(annotation.Floci, floci.Resource)))
            {
                enabled.Add(resource);
            }
        }

        bool added;
        do
        {
            added = false;
            foreach (var resource in builder.Resources)
            {
                if (enabled.Contains(resource) || resource is not (IResourceWithEnvironment and IResourceWithWaitSupport) ||
                    !resource.Annotations.OfType<ResourceRelationshipAnnotation>().Any(annotation => enabled.Contains(annotation.Resource)))
                {
                    continue;
                }

                builder.CreateResourceBuilder((IResourceWithEnvironment)resource).WithReference(floci);
                enabled.Add(resource);
                added = true;
            }
        } while (added);
    }
}

internal sealed class FlociAwsSdkConfig(RegionEndpoint region) : IAWSSDKConfig
{
    public string? Profile { get; set; }
    public RegionEndpoint? Region { get; set; } = region;
    public bool SDKValidationEnabled { get; set; }
}
