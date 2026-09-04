using Amazon.CDK;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Floci.AWS;

namespace Aspire.Hosting;

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIREPIPELINES001

/// <summary>Provides extensions that deploy an AWS CDK environment to Floci.</summary>
public static class FlociDeploymentExtensions
{
    /// <summary>
    /// Configures an AWS CDK environment to deploy to a running Floci endpoint.
    /// </summary>
    [AspireExportIgnore(Reason = "AWS CDK deployment environments are not available in polyglot AppHosts.")]
    public static IResourceBuilder<AWSCDKEnvironmentResource<TStack>> WithFlociDeploymentTarget<TStack>(
        this IResourceBuilder<AWSCDKEnvironmentResource<TStack>> builder,
        Uri endpoint,
        FlociAwsOptions? options = null)
        where TStack : Stack
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The Floci endpoint must be an absolute HTTP or HTTPS URI.", nameof(endpoint));
        }

        return Configure(builder, endpoint, options ?? new FlociAwsOptions());
    }

    private static IResourceBuilder<AWSCDKEnvironmentResource<TStack>> Configure<TStack>(
        IResourceBuilder<AWSCDKEnvironmentResource<TStack>> builder,
        Uri endpoint,
        FlociAwsOptions options)
        where TStack : Stack
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Region);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AccessKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SecretAccessKey);

        var annotation = new FlociDeploymentTargetAnnotation(endpoint, Clone(options));
        var alreadyConfigured = builder.Resource.TryGetLastAnnotation<FlociDeploymentTargetAnnotation>(out _);
        builder.WithAnnotation(annotation, ResourceAnnotationMutationBehavior.Replace);

        if (!alreadyConfigured)
        {
            builder.WithPipelineStepFactory(_ => CreateSteps(builder.Resource));
            builder.WithPipelineConfiguration(context =>
            {
                if (!builder.Resource.TryGetLastAnnotation<FlociDeploymentTargetAnnotation>(out var target))
                {
                    return Task.CompletedTask;
                }

                ConfigureStepOrder(context, builder.Resource.Name);
                return Task.CompletedTask;
            });
        }

        return builder;
    }

    internal static IEnumerable<PipelineStep> CreateSteps(AWSCDKEnvironmentResource environment)
    {
        var target = environment.Annotations.OfType<FlociDeploymentTargetAnnotation>().Last();
        foreach (var operation in new[] { "deploy", "destroy" })
        {
            yield return new PipelineStep
            {
                Name = $"configure-floci-{operation}-{environment.Name}",
                Description = $"Configures the AWS {operation} step to target Floci",
                Resource = environment,
                Action = _ =>
                {
                    target.PreviousEnvironment = FlociDeploymentEnvironment.Apply(target.Endpoint, target.Options);
                    return Task.CompletedTask;
                }
            };

            yield return new PipelineStep
            {
                Name = $"restore-floci-{operation}-{environment.Name}",
                Description = $"Restores AWS settings after the Floci {operation} step",
                Resource = environment,
                Action = _ =>
                {
                    if (target.PreviousEnvironment is { } previous)
                    {
                        FlociDeploymentEnvironment.Restore(previous);
                        target.PreviousEnvironment = null;
                    }

                    return Task.CompletedTask;
                }
            };
        }
    }

    internal static void ConfigureStepOrder(PipelineConfigurationContext context, string environmentName)
    {
        foreach (var operation in new[] { "deploy", "destroy" })
        {
            var awsStepName = $"{operation}-{environmentName}";
            var configureStepName = $"configure-floci-{operation}-{environmentName}";
            var restoreStepName = $"restore-floci-{operation}-{environmentName}";
            var awsStep = context.Steps.FirstOrDefault(step => step.Name == awsStepName)
                ?? throw new DistributedApplicationException($"AWS CDK pipeline step '{awsStepName}' was not found.");
            var restoreStep = context.Steps.FirstOrDefault(step => step.Name == restoreStepName)
                ?? throw new DistributedApplicationException($"Floci pipeline step '{restoreStepName}' was not found.");

            awsStep.DependsOn(configureStepName);
            restoreStep.DependsOn(awsStepName);
            restoreStep.RequiredBy(operation == "deploy" ? WellKnownPipelineSteps.Deploy : WellKnownPipelineSteps.Destroy);
        }
    }

    private static FlociAwsOptions Clone(FlociAwsOptions options) => new()
    {
        Region = options.Region,
        AccessKeyId = options.AccessKeyId,
        SecretAccessKey = options.SecretAccessKey,
        SessionToken = options.SessionToken
    };
}
