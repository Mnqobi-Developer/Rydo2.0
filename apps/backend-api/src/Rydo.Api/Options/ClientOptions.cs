using System.ComponentModel.DataAnnotations;

namespace Rydo.Api.Options;

public sealed class ClientOptions
{
    public const string SectionName = "Clients";

    [Required]
    [MinLength(1)]
    public string[] AllowedOrigins { get; init; } = [];
}
