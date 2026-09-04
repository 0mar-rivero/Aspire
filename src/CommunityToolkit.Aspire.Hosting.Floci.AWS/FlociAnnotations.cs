using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS;

internal sealed class FlociEnabledAnnotation(FlociAwsContainerResource floci) : IResourceAnnotation
{
    public FlociAwsContainerResource Floci { get; } = floci;
}

internal sealed class FlociPropagationAnnotation : IResourceAnnotation;

internal sealed record FlociDeploymentTargetAnnotation(
    Uri Endpoint,
    FlociAwsOptions Options) : IResourceAnnotation
{
    internal IReadOnlyDictionary<string, string?>? PreviousEnvironment { get; set; }
}
