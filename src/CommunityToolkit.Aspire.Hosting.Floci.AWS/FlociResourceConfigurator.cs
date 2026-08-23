using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CloudFormation;
using System.Collections.Concurrent;

namespace CommunityToolkit.Aspire.Hosting.Floci.AWS;

internal static class FlociResourceConfigurator
{
    internal static void ConfigureCloudFormation(ICloudFormationResource resource, Uri endpoint, FlociAwsOptions options)
    {
        AWSCredentials credentials = string.IsNullOrWhiteSpace(options.SessionToken)
            ? new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey)
            : new SessionAWSCredentials(options.AccessKeyId, options.SecretAccessKey, options.SessionToken);

        resource.CloudFormationClient = new FlociCloudFormationClient(credentials, new AmazonCloudFormationConfig
        {
            ServiceURL = endpoint.ToString(),
            AuthenticationRegion = options.Region
        });
    }
}

internal sealed class FlociCloudFormationClient(AWSCredentials credentials, AmazonCloudFormationConfig config)
    : AmazonCloudFormationClient(credentials, config)
{
    private readonly ConcurrentDictionary<string, string> _stackNamesByChangeSet = new(StringComparer.Ordinal);

    public override async Task<CreateChangeSetResponse> CreateChangeSetAsync(CreateChangeSetRequest request, CancellationToken cancellationToken = default)
    {
        var response = await base.CreateChangeSetAsync(request, cancellationToken).ConfigureAwait(false);
        response.Id ??= request.ChangeSetName;
        _stackNamesByChangeSet[response.Id] = request.StackName;
        return response;
    }

    public override Task<DescribeChangeSetResponse> DescribeChangeSetAsync(DescribeChangeSetRequest request, CancellationToken cancellationToken = default)
    {
        if (request.StackName is null && request.ChangeSetName is not null &&
            _stackNamesByChangeSet.TryGetValue(request.ChangeSetName, out var stackName))
        {
            request.StackName = stackName;
        }

        return base.DescribeChangeSetAsync(request, cancellationToken);
    }
}
