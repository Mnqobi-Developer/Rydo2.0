namespace Rydo.Infrastructure.Admin;

public sealed class AdminAccessOptions
{
    public const string SectionName = "AdminAccess";

    public bool Enabled { get; init; }

    public string BootstrapEmail { get; init; } = string.Empty;

    public string BootstrapPhoneNumber { get; init; } = string.Empty;

    public string BootstrapPassword { get; init; } = string.Empty;

    public bool IsValid()
    {
        return !Enabled ||
            (BootstrapEmail.Length is > 3 and <= 254 &&
                BootstrapEmail.Contains('@') &&
                BootstrapPhoneNumber.Length is >= 8 and <= 16 &&
                BootstrapPhoneNumber.StartsWith('+') &&
                BootstrapPhoneNumber[1..].All(char.IsDigit) &&
                BootstrapPassword.Length is >= 16 and <= 200);
    }
}
