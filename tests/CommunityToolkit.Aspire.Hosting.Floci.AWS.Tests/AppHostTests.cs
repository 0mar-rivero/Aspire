using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS.Tests;

[RequiresDocker]
[Collection(FlociAwsAppHostCollection.Name)]
public sealed class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Floci_AWS_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Floci_AWS_AppHost>>
{
    [Fact]
    public async Task CdkStackCreatesQueueInFloci()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        await fixture.ResourceNotificationService
            .WaitForResourceAsync("queue-stack", KnownResourceStates.Running, timeout.Token);

        var endpoint = await fixture.App.GetConnectionStringAsync("floci", cancellationToken: timeout.Token);
        Assert.NotNull(endpoint);

        using var sqs = new AmazonSQSClient(new BasicAWSCredentials("test", "test"), new AmazonSQSConfig
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1"
        });

        var queues = await sqs.ListQueuesAsync(new ListQueuesRequest(), timeout.Token);
        Assert.Contains(queues.QueueUrls, queueUrl => queueUrl.EndsWith("/aspire-floci-demo", StringComparison.Ordinal));
    }
}