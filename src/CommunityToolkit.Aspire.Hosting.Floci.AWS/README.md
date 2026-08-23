# CommunityToolkit.Aspire.Hosting.Floci.AWS

An Aspire hosting integration that routes the official `Aspire.Hosting.AWS` CloudFormation and CDK resources through the [Floci](https://github.com/floci-io/floci) local AWS emulator.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var aws = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.USEast1);
var floci = builder.AddFlociAws("floci", defaultRegion: "us-east-1");

builder.AddAWSCloudFormationTemplate("resources", "resources.yaml")
    .WithReference(aws)
    .WithReference(floci);

builder.Build().Run();
```

This package extends the Floci AWS resource from `CommunityToolkit.Aspire.Hosting.Floci`; it does not replace its `AddFlociAws` API. Call `WithReference(floci)` on each CloudFormation template or CDK stack that should run locally. Projects, executables, and containers that depend on those resources automatically receive the Floci endpoint and AWS environment variables.

The same reference works for imported stacks created with `AddAWSCloudFormationStack`. CloudFormation and CDK integration is currently C#-only, matching the API surface exposed by `Aspire.Hosting.AWS`.

The AWS SDK clients used internally by CDK for STS and S3 asset uploads are configured process-wide. Use one Floci endpoint for CDK deployment per AppHost; overlapping CDK deployments targeting different Floci endpoints, or mixing Floci-backed and real-AWS CDK deployments in the same AppHost, are not supported.

Configure the emulator with the existing Floci extensions when needed:

```csharp
var floci = builder.AddFlociAws("floci")
    .WithDockerSocket()
    .WithDataVolume("floci-data");
```
