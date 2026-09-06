using Amazon;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.Deployment;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Floci.AWS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS.Tests;

#pragma warning disable ASPIREAWSPUBLISHERS001
#pragma warning disable ASPIREPIPELINES001

[Collection(nameof(FlociDeploymentEnvironmentCollection))]
public sealed class FlociDeploymentExtensionsTests
{
    [Fact]
    public void DeploymentStackCanRunLocallyAndDeployToExplicitFlociEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        var aws = builder.AddAWSSDKConfig()
            .WithRegion(RegionEndpoint.USEast1)
            .WithSdkValidation(false);
        var floci = builder.AddFlociAws("floci", port: 4566, defaultRegion: "us-east-1");
        var environment = builder.AddAWSCDKEnvironment(
                "aws",
                CDKDefaultsProviderFactory.Preview_V1,
                environmentResourceConfig: new AWSCDKEnvironmentResourceConfig { AWSSDKConfig = aws })
            .WithFlociDeploymentTarget(new Uri("http://localhost:4566"));

        var stack = environment.UseDeploymentStack("aws")
            .WithReference(aws)
            .WithReference(floci);
        stack.AddSQSQueue("orders");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Contains(model.Resources, resource => resource.Name == "aws-stack");
        Assert.Contains(model.Resources, resource => resource.Name == "orders");
        Assert.Single(environment.Resource.Annotations.OfType<FlociDeploymentTargetAnnotation>());
    }

    [Fact]
    public async Task ExplicitEndpointScopesFlociEnvironmentToDeployAndDestroy()
    {
        var builder = CreatePublishBuilder();
        var environment = builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1)
            .WithFlociDeploymentTarget(
                new Uri("http://localhost:4566/"),
                new FlociAwsOptions
                {
                    Region = "eu-west-1",
                    AccessKeyId = "floci-key",
                    SecretAccessKey = "floci-secret"
                });
        var observed = new List<(string? Endpoint, string? Region, string? AccessKey)>();
        var steps = CreateSteps(environment.Resource.Name, observed);

        using var app = builder.Build();
        steps = await ConfigurePipelineAsync(environment.Resource, app.Services, steps);
        var stepContext = CreateStepContext(app.Services);

        const string originalEndpoint = "https://original.example";
        var previousEndpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", originalEndpoint);
        try
        {
            foreach (var operation in new[] { "deploy", "destroy" })
            {
                await steps.Single(step => step.Name == $"configure-floci-{operation}-aws").Action(stepContext);
                await steps.Single(step => step.Name == $"{operation}-aws").Action(stepContext);
                await steps.Single(step => step.Name == $"restore-floci-{operation}-aws").Action(stepContext);
            }

            Assert.All(observed, value =>
            {
                Assert.Equal("http://localhost:4566", value.Endpoint);
                Assert.Equal("eu-west-1", value.Region);
                Assert.Equal("floci-key", value.AccessKey);
            });
            Assert.Equal(originalEndpoint, Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL"));
            Assert.Contains("configure-floci-deploy-aws", steps.Single(step => step.Name == "deploy-aws").DependsOnSteps);
            Assert.Contains("deploy-aws", steps.Single(step => step.Name == "restore-floci-deploy-aws").DependsOnSteps);
            Assert.Contains(WellKnownPipelineSteps.Deploy,
                steps.Single(step => step.Name == "restore-floci-deploy-aws").RequiredBySteps);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", previousEndpoint);
        }
    }

    [Fact]
    public void RelativeEndpointIsRejected()
    {
        var builder = CreatePublishBuilder();
        var environment = builder.AddAWSCDKEnvironment("aws", CDKDefaultsProviderFactory.Preview_V1);

        Assert.Throws<ArgumentException>(() =>
            environment.WithFlociDeploymentTarget(new Uri("floci", UriKind.Relative)));
    }

    private static IDistributedApplicationBuilder CreatePublishBuilder() =>
        DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", "--step", "publish"]
        });

    private static List<PipelineStep> CreateSteps(
        string environmentName,
        List<(string? Endpoint, string? Region, string? AccessKey)> observed)
    {
        Task Observe(PipelineStepContext _)
        {
            observed.Add((
                Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL"),
                Environment.GetEnvironmentVariable("AWS_REGION"),
                Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")));
            return Task.CompletedTask;
        }

        return
        [
            new() { Name = $"deploy-{environmentName}", Action = Observe },
            new() { Name = $"destroy-{environmentName}", Action = Observe }
        ];
    }

    private static async Task<List<PipelineStep>> ConfigurePipelineAsync(
        AWSCDKEnvironmentResource resource,
        IServiceProvider services,
        List<PipelineStep> steps)
    {
        steps.AddRange(FlociDeploymentExtensions.CreateSteps(resource));
        var annotation = Assert.Single(resource.Annotations.OfType<PipelineConfigurationAnnotation>());
        var context = new PipelineConfigurationContext
        {
            Model = services.GetRequiredService<DistributedApplicationModel>(),
            Services = services,
            Steps = steps
        };
        await annotation.Callback(context);
        return [.. context.Steps];
    }

    private static PipelineStepContext CreateStepContext(IServiceProvider services)
    {
        var model = services.GetRequiredService<DistributedApplicationModel>();
        return new PipelineStepContext
        {
            PipelineContext = new PipelineContext(
                model,
                new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
                services,
                NullLogger.Instance,
                default),
            ReportingStep = null!
        };
    }
}

[CollectionDefinition(nameof(FlociDeploymentEnvironmentCollection), DisableParallelization = true)]
public sealed class FlociDeploymentEnvironmentCollection;
