using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Rydo.Infrastructure.Payments;

public static class PayFastSignature
{
    public static string Generate(
        IEnumerable<KeyValuePair<string, string>> fields,
        string passphrase)
    {
        var pairs = fields
            .Where(field => field.Key != "signature" &&
                !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => $"{field.Key}={Encode(field.Value.Trim())}")
            .ToList();

        if (!string.IsNullOrWhiteSpace(passphrase))
        {
            pairs.Add($"passphrase={Encode(passphrase.Trim())}");
        }

#pragma warning disable CA5351 // PayFast's custom integration protocol mandates MD5 signatures.
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(string.Join('&', pairs)));
#pragma warning restore CA5351
        return Convert.ToHexStringLower(bytes);
    }

    public static string Encode(string value)
    {
        var builder = new StringBuilder();

        foreach (var item in Encoding.UTF8.GetBytes(value))
        {
            if ((item >= 'a' && item <= 'z') ||
                (item >= 'A' && item <= 'Z') ||
                (item >= '0' && item <= '9') ||
                item is (byte)'-' or (byte)'_' or (byte)'.')
            {
                builder.Append((char)item);
            }
            else if (item == ' ')
            {
                builder.Append('+');
            }
            else
            {
                builder.Append('%').Append(item.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }
}
