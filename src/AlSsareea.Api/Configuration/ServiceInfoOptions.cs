namespace AlSsareea.Api.Configuration;

public sealed class ServiceInfoOptions
{
    public const string SectionName = "ServiceInfo";

    public string Service { get; init; } = "AlSsareea.Backend";

    public string ApiVersion { get; init; } = "1.0";
}
