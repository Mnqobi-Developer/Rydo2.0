using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Rydo.Application.Payments;
using Rydo.Domain.Payments;

namespace Rydo.Infrastructure.Payments;

public sealed class PayFastGateway(
    IOptions<PayFastOptions> options,
    PayFastHttpClient httpClient) : IPayFastGateway
{
    private readonly PayFastOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public PayFastCheckout CreateCheckout(
        Payment payment,
        string firstName,
        string lastName,
        string? email,
        string phoneNumber)
    {
        if (!IsConfigured)
        {
            throw new PaymentProviderUnavailableException();
        }

        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = _options.MerchantId,
            ["merchant_key"] = _options.MerchantKey,
            ["return_url"] = _options.ReturnUrl,
            ["cancel_url"] = _options.CancelUrl,
            ["notify_url"] = _options.NotifyUrl,
            ["name_first"] = firstName,
            ["name_last"] = lastName,
            ["email_address"] = email ?? string.Empty,
            ["cell_number"] = phoneNumber.TrimStart('+'),
            ["m_payment_id"] = payment.Id.ToString(),
            ["amount"] = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["item_name"] = $"RYDO trip {payment.TripId:N}",
            ["item_description"] = "RYDO passenger trip payment",
        };
        fields["signature"] = PayFastSignature.Generate(fields, _options.Passphrase);
        return new PayFastCheckout(_options.ProcessUrl, fields);
    }

    public async Task<PayFastValidationResult> ValidateNotificationAsync(
        IReadOnlyList<KeyValuePair<string, string>> fields,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return PayFastValidationResult.Invalid("PayFast is not configured.");
        }

        var submittedSignature = fields.FirstOrDefault(
            field => field.Key == "signature").Value;
        var expectedSignature = PayFastSignature.Generate(fields, _options.Passphrase);

        if (!FixedTimeEquals(submittedSignature, expectedSignature))
        {
            return PayFastValidationResult.Invalid("Invalid PayFast signature.");
        }

        if (fields.FirstOrDefault(field => field.Key == "merchant_id").Value !=
            _options.MerchantId)
        {
            return PayFastValidationResult.Invalid("PayFast merchant does not match.");
        }

        if (remoteIpAddress is null || !_options.AllowedIpNetworks.Any(
            network => Contains(network, remoteIpAddress)))
        {
            return PayFastValidationResult.Invalid("PayFast source address is not allowed.");
        }

        var parameterString = string.Join('&', fields
            .TakeWhile(field => field.Key != "signature")
            .Where(field => !string.IsNullOrEmpty(field.Value))
            .Select(field => $"{field.Key}={PayFastSignature.Encode(field.Value)}"));

        using var content = new StringContent(
            parameterString,
            Encoding.UTF8,
            "application/x-www-form-urlencoded");
        using var response = await httpClient.Client.PostAsync(
            _options.ValidationUrl,
            content,
            cancellationToken);
        var confirmation = await response.Content.ReadAsStringAsync(cancellationToken);

        return response.IsSuccessStatusCode && confirmation.Trim() == "VALID"
            ? PayFastValidationResult.Valid()
            : PayFastValidationResult.Invalid("PayFast server confirmation failed.");
    }

    private static bool FixedTimeEquals(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || actual.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(expected));
    }

    private static bool Contains(string network, IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var parts = network.Split('/');
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var baseAddress) ||
            !int.TryParse(parts[1], out var prefixLength) ||
            prefixLength is < 0 or > 32)
        {
            return false;
        }

        var addressValue = ToUInt32(address);
        var baseValue = ToUInt32(baseAddress);
        var mask = prefixLength == 0 ? 0U : uint.MaxValue << (32 - prefixLength);
        return (addressValue & mask) == (baseValue & mask);
    }

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) |
            ((uint)bytes[1] << 16) |
            ((uint)bytes[2] << 8) |
            bytes[3];
    }
}

public sealed class PayFastHttpClient
{
    public HttpClient Client { get; } = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };
}
