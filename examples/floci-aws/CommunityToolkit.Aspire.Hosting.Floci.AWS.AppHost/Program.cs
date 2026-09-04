using Amazon;
using Amazon.CDK.AWS.SQS;
using Aspire.Hosting.AWS.Deployment;

#pragma warning disable ASPIREAWSPUBLISHERS001

var builder = DistributedApplication.CreateBuilder(args);

var aws = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.USEast1);
var floci = builder.AddFlociAws("floci", port: 4566, defaultRegion: "us-east-1");

builder.AddAWSCDKEnvironment("deployment", CDKDefaultsProviderFactory.Preview_V1)
    .WithFlociDeploymentTarget(new Uri("http://localhost:4566"));

var stack = builder.AddAWSCDKStack("queue-stack")
    .WithReference(aws)
    .WithReference(floci);

stack.AddConstruct("queue", scope => new Queue(scope, "Queue", new QueueProps
{
    QueueName = "aspire-floci-demo"
}));

builder.Build().Run();
