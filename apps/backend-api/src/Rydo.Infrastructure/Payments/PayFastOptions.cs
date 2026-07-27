namespace Rydo.Infrastructure.Payments;

public sealed class PayFastOptions
{
    public const string SectionName = "PayFast";

    public bool Enabled { get; init; }

    public bool Sandbox { get; init; } = true;

    public string MerchantId { get; init; } = string.Empty;

    public string MerchantKey { get; init; } = string.Empty;

    public string Passphrase { get; init; } = string.Empty;

    public string ReturnUrl { get; init; } = string.Empty;

    public string CancelUrl { get; init; } = string.Empty;

    public string NotifyUrl { get; init; } = string.Empty;

    public string ProcessUrl => Sandbox
        ? "https://sandbox.payfast.co.za/eng/process"
        : "https://www.payfast.co.za/eng/process";

    public string ValidationUrl => Sandbox
        ? "https://sandbox.payfast.co.za/eng/query/validate"
        : "https://www.payfast.co.za/eng/query/validate";

    public string[] AllowedIpNetworks { get; init; } =
    [
        "197.97.145.144/28",
        "41.74.179.192/27",
        "102.216.36.0/28",
        "102.216.36.128/28",
        "144.126.193.139/32",
    ];

    public bool IsConfigured => Enabled &&
        !string.IsNullOrWhiteSpace(MerchantId) &&
        !string.IsNullOrWhiteSpace(MerchantKey) &&
        !string.IsNullOrWhiteSpace(Passphrase) &&
        IsPublicHttpsUrl(ReturnUrl) &&
        IsPublicHttpsUrl(CancelUrl) &&
        IsPublicHttpsUrl(NotifyUrl);

    private static bool IsPublicHttpsUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            !uri.IsLoopback;
    }
}
