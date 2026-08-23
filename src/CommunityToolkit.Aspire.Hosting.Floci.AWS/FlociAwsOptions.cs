namespace CommunityToolkit.Aspire.Hosting.Floci.AWS;

/// <summary>Settings used by AWS clients that are redirected to Floci.</summary>
public sealed class FlociAwsOptions
{
    /// <summary>Gets or sets the AWS region.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Gets or sets the access key ID.</summary>
    public string AccessKeyId { get; set; } = "test";

    /// <summary>Gets or sets the secret access key.</summary>
    public string SecretAccessKey { get; set; } = "test";

    /// <summary>Gets or sets an optional session token.</summary>
    public string? SessionToken { get; set; }
}
