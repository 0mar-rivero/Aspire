using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Internal;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Internal;
using CommunityToolkit.Aspire.Hosting.Floci.AWS;
using System.Reflection;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS.Tests;

[Collection(AwsSdkGlobalStateCollection.Name)]
public sealed class FlociCdkRuntimeTests
{
    [Fact]
    public async Task CredentialOverrideInstallsFlociCredentials()
    {
        var previous = AWSConfigs.AWSCredentialsGenerators;
        var options = new FlociAwsOptions
        {
            AccessKeyId = "access-key",
            SecretAccessKey = "secret-key",
            SessionToken = "session-token"
        };

        try
        {
            FlociCdkCredentialsOverride.Apply(options);

            var generator = Assert.Single(AWSConfigs.AWSCredentialsGenerators!);
            var credentials = await generator().GetCredentialsAsync();
            Assert.Equal(options.AccessKeyId, credentials.AccessKey);
            Assert.Equal(options.SecretAccessKey, credentials.SecretKey);
            Assert.Equal(options.SessionToken, credentials.Token);
        }
        finally
        {
            AWSConfigs.AWSCredentialsGenerators = previous;
        }
    }

    [Fact]
    public void RegistrationPositionsS3HandlersAroundEndpointResolver()
    {
        FlociCdkAssetUploadEndpointCustomizer.Deregister();
        try
        {
            FlociCdkAssetUploadEndpointCustomizer.Register(new Uri("http://localhost:4566"));
            using var client = new AmazonS3Client(
                new BasicAWSCredentials("test", "test"),
                new AmazonS3Config { ServiceURL = "http://localhost:4566", ForcePathStyle = true });

            var handlers = GetRuntimePipeline(client).EnumerateHandlers().ToList();
            var resolverIndex = handlers.FindIndex(static handler => handler is AmazonS3EndpointResolver);

            Assert.True(resolverIndex >= 0);
            Assert.IsType<FlociCdkAssetUploadForcePathStyleHandler>(handlers[resolverIndex - 1]);
            Assert.IsType<FlociCdkAssetUploadEndpointRedirectHandler>(handlers[resolverIndex + 1]);
        }
        finally
        {
            FlociCdkAssetUploadEndpointCustomizer.Deregister();
        }
    }

    [Fact]
    public void RegistrationPositionsRedirectAfterStsEndpointResolver()
    {
        FlociCdkAssetUploadEndpointCustomizer.Deregister();
        try
        {
            FlociCdkAssetUploadEndpointCustomizer.Register(new Uri("http://localhost:4566"));
            using var client = new AmazonSecurityTokenServiceClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonSecurityTokenServiceConfig { ServiceURL = "http://localhost:4566" });

            var handlers = GetRuntimePipeline(client).EnumerateHandlers().ToList();
            var resolverIndex = handlers.FindIndex(static handler => handler is AmazonSecurityTokenServiceEndpointResolver);

            Assert.True(resolverIndex >= 0);
            Assert.IsType<FlociCdkAssetUploadEndpointRedirectHandler>(handlers[resolverIndex + 1]);
        }
        finally
        {
            FlociCdkAssetUploadEndpointCustomizer.Deregister();
        }
    }

    private static RuntimePipeline GetRuntimePipeline(AmazonServiceClient client)
    {
        var property = typeof(AmazonServiceClient).GetProperty(
            "RuntimePipeline",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AmazonServiceClient.RuntimePipeline property was not found.");

        return Assert.IsType<RuntimePipeline>(property.GetValue(client));
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AwsSdkGlobalStateCollection
{
    public const string Name = "AWS SDK global state";
}