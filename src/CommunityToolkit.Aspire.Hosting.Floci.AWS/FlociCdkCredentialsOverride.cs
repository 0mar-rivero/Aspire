using Amazon;
using Amazon.Runtime;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS;

/// <summary>Installs the Floci credentials used by the AWS CDK asset uploader.</summary>
/// <remarks>
/// The CDK asset uploader creates its own STS and S3 clients and resolves their credentials through
/// the default AWS SDK credential chain. The credential generator is process-wide by AWS SDK design;
/// while a Floci-backed CDK stack is active, AppHost-side default credential resolution uses Floci's
/// credentials. Mixing Floci-backed and real-AWS CDK deployments in one AppHost is not supported.
/// </remarks>
internal static class FlociCdkCredentialsOverride
{
    internal static void Apply(FlociAwsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        AWSConfigs.AWSCredentialsGenerators =
        [
            () => string.IsNullOrWhiteSpace(options.SessionToken)
                ? new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey)
                : new SessionAWSCredentials(options.AccessKeyId, options.SecretAccessKey, options.SessionToken)
        ];
    }
}
