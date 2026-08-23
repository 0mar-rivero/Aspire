using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS;

internal sealed class FlociEnabledAnnotation(FlociAwsContainerResource floci) : IResourceAnnotation
{
    public FlociAwsContainerResource Floci { get; } = floci;
}

internal sealed class FlociPropagationAnnotation : IResourceAnnotation;
