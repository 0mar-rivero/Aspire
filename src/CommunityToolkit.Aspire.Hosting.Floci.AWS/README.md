# CommunityToolkit.Aspire.Hosting.Floci.AWS

An Aspire hosting integration that routes the `Habichuelo.Aspire.Hosting.Aws` CloudFormation and CDK resources through the [Floci](https://github.com/floci-io/floci) local AWS emulator.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var aws = builder.AddAWSSDKConfig()
    .WithRegion(RegionEndpoint.USEast1)
    .WithSdkValidation(false);
var floci = builder.AddFlociAws("floci", defaultRegion: "us-east-1");

builder.AddAWSCloudFormationTemplate("resources", "resources.yaml")
    .WithReference(aws)
    .WithReference(floci);

builder.Build().Run();
```

This package extends the Floci AWS resource from `CommunityToolkit.Aspire.Hosting.Floci`; it does not replace its `AddFlociAws` API. Call `WithReference(floci)` on each CloudFormation template or CDK stack that should run locally. Projects, executables, and containers that depend on those resources automatically receive the Floci endpoint and AWS environment variables.

The same reference works for imported stacks created with `AddAWSCloudFormationStack`. CloudFormation and CDK integration is currently C#-only, matching the API surface exposed by `Habichuelo.Aspire.Hosting.Aws`.

## Deploying an AWS CDK environment to Floci

An `AWSCDKEnvironmentResource` normally invokes the CDK CLI against AWS. Use
`WithFlociDeploymentTarget` to scope the deploy and destroy steps to a running Floci endpoint:

```csharp
var aws = builder.AddAWSSDKConfig()
    .WithRegion(RegionEndpoint.USEast1)
    .WithSdkValidation(false);
var floci = builder.AddFlociAws("floci", port: 4566, defaultRegion: "us-east-1");

var deployment = builder.AddAWSCDKEnvironment(
        "my-app",
        CDKDefaultsProviderFactory.Preview_V1,
        environmentResourceConfig: new AWSCDKEnvironmentResourceConfig { AWSSDKConfig = aws })
    .WithFlociDeploymentTarget(
        new Uri("http://localhost:4566"),
        new FlociAwsOptions
        {
            Region = "us-east-1",
            AccessKeyId = "test",
            SecretAccessKey = "test"
        });

var stack = deployment.UseDeploymentStack("my-app")
    .WithReference(aws)
    .WithReference(floci);

stack.AddSQSQueue("orders");
```

`UseDeploymentStack` keeps the constructs added through the stack APIs in the CDK environment's
deployment stack. The companion stack resource runs the same construct graph locally against Floci,
while `WithFlociDeploymentTarget` redirects CDK deployment to the explicit emulator URL.
The explicit run stack name matches the deployment stack name because the two stacks live at
different endpoints, so they cannot collide.

The endpoint is deliberately explicit because deployment runs outside normal resource orchestration;
this helper does not start or own a Floci resource. Start Floci first, then invoke deployment:

```text
aspire start
aspire deploy
```

This is an existing-target deployer: it does not start or own the Floci process. The helper applies
the Floci endpoint, region, and placeholder credentials only while the CDK deploy or destroy action
runs, then restores the AppHost process environment. Publish remains local CDK synthesis and does
not contact Floci.

The AWS SDK clients used internally by CDK for STS and S3 asset uploads are configured process-wide. Use one Floci endpoint for CDK deployment per AppHost; overlapping CDK deployments targeting different Floci endpoints, or mixing Floci-backed and real-AWS CDK deployments in the same AppHost, are not supported.

Configure the emulator with the existing Floci extensions when needed:

```csharp
var floci = builder.AddFlociAws("floci")
    .WithDockerSocket()
    .WithDataVolume("floci-data");
```
