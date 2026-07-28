namespace Rydo.Infrastructure.Maps;

public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    public string ServerApiKey { get; init; } = string.Empty;
}
