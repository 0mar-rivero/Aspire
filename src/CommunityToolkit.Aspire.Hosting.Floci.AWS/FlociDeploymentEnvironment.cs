using CommunityToolkit.Aspire.Hosting.Floci.AWS;

namespace Aspire.Hosting;

internal static class FlociDeploymentEnvironment
{
    private static readonly string[] EndpointVariables =
    [
        "AWS_ENDPOINT_URL",
        "AWS_ENDPOINT_URL_CLOUDFORMATION",
        "AWS_ENDPOINT_URL_ECR",
        "AWS_ENDPOINT_URL_S3",
        "AWS_ENDPOINT_URL_STS"
    ];

    internal static IReadOnlyDictionary<string, string?> Apply(Uri endpoint, FlociAwsOptions options)
    {
        var values = CreateValues(endpoint, options);
        var previous = values.Keys.ToDictionary(
            static name => name,
            static name => Environment.GetEnvironmentVariable(name),
            StringComparer.Ordinal);

        foreach (var (name, value) in values)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        return previous;
    }

    internal static void Restore(IReadOnlyDictionary<string, string?> previous)
    {
        foreach (var (name, value) in previous)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    internal static IReadOnlyDictionary<string, string?> CreateValues(Uri endpoint, FlociAwsOptions options)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var name in EndpointVariables)
        {
            values[name] = endpoint.AbsoluteUri.TrimEnd('/');
        }

        values["AWS_ACCESS_KEY_ID"] = options.AccessKeyId;
        values["AWS_SECRET_ACCESS_KEY"] = options.SecretAccessKey;
        values["AWS_SESSION_TOKEN"] = options.SessionToken;
        values["AWS_REGION"] = options.Region;
        values["AWS_DEFAULT_REGION"] = options.Region;
        values["AWS_EC2_METADATA_DISABLED"] = "true";
        return values;
    }
}
