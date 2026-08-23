using Amazon;
using Amazon.CDK.AWS.SQS;

var builder = DistributedApplication.CreateBuilder(args);

var aws = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.USEast1);
var floci = builder.AddFlociAws("floci", defaultRegion: "us-east-1");

var stack = builder.AddAWSCDKStack("queue-stack")
    .WithReference(aws)
    .WithReference(floci);

stack.AddConstruct("queue", scope => new Queue(scope, "Queue", new QueueProps
{
    QueueName = "aspire-floci-demo"
}));

builder.Build().Run();
