using Amazon;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci.AWS;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS.Tests;

public sealed class FlociCloudFormationExtensionsTests
{
    [Fact]
    public void WithReferenceTracksFlociDependency()
    {
        var builder = DistributedApplication.CreateBuilder();
        var floci = builder.AddFlociAws("floci");
        var stack = builder.AddAWSCloudFormationTemplate("stack", "template.yaml").WithReference(floci);

        Assert.Contains(stack.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            annotation => ReferenceEquals(annotation.Resource, floci.Resource));
        Assert.Contains(stack.Resource.Annotations, annotation => annotation is FlociEnabledAnnotation);
    }

    [Fact]
    public void AwsConfigRegionIsAcceptedByReference()
    {
        var builder = DistributedApplication.CreateBuilder();
        var aws = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUWest1);
        var floci = builder.AddFlociAws("floci");
        var options = new FlociAwsOptions { Region = "us-west-2" };

        var stack = builder.AddAWSCloudFormationTemplate("stack", "template.yaml")
            .WithReference(floci, options, aws);

        Assert.Contains(stack.Resource.Annotations, annotation => annotation is FlociEnabledAnnotation);
        Assert.Equal("us-west-2", options.Region);
    }

    [Fact]
    public void ImportedStackCanReferenceFloci()
    {
        var builder = DistributedApplication.CreateBuilder();
        var floci = builder.AddFlociAws("floci");
        var stack = builder.AddAWSCloudFormationStack("remote-stack").WithReference(floci);

        Assert.Contains(stack.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            annotation => ReferenceEquals(annotation.Resource, floci.Resource));
        Assert.Contains(stack.Resource.Annotations, annotation => annotation is FlociEnabledAnnotation);
    }

    [Fact]
    public void ExplicitStackReferencePropagatesToDependentResources()
    {
        var builder = DistributedApplication.CreateBuilder();
        var floci = builder.AddFlociAws("floci");
        var stack = builder.AddAWSCloudFormationTemplate("stack", "template.yaml").WithReference(floci);
        var consumer = builder.AddContainer("consumer", "busybox").WithReference(stack);

        FlociCloudFormationExtensions.PropagateFlociReferences(builder, floci);

        Assert.Contains(consumer.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            annotation => ReferenceEquals(annotation.Resource, floci.Resource));
        Assert.NotEmpty(consumer.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>());
    }

    [Fact]
    public async Task DynamoDbLocalOverridesFlociOnlyForDynamoDb()
    {
        var builder = DistributedApplication.CreateBuilder();
        var floci = builder.AddFlociAws("floci");
        var dynamoDb = builder.AddAWSDynamoDBLocal("dynamodb");
        var stack = builder.AddAWSCloudFormationTemplate("stack", "template.yaml").WithReference(floci);
        var consumer = builder.AddContainer("consumer", "busybox")
            .WithReference(stack)
            .WithReference(dynamoDb);

        FlociCloudFormationExtensions.PropagateFlociReferences(builder, floci);

        var environment = await ResolveEnvironmentAsync(builder, consumer);
        Assert.Contains("AWS_ENDPOINT_URL", environment);
        Assert.Contains("AWS_ENDPOINT_URL_DYNAMODB", environment);
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        IResourceBuilder<FlociAwsContainerResource> floci = null!;
        var builder = DistributedApplication.CreateBuilder();
        var stack = builder.AddAWSCloudFormationTemplate("stack", "template.yaml");

        Assert.Throws<ArgumentNullException>(() => stack.WithReference(floci));
    }

    private static async Task<Dictionary<string, object>> ResolveEnvironmentAsync<T>(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<T> resource)
        where T : IResource
    {
        var environment = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, environment);
        foreach (var annotation in resource.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return environment;
    }
}