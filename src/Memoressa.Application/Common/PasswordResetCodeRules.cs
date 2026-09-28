using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Memoressa.Application.Common;

public static partial class PasswordResetCodeRules
{
    public const int CodeLength = 8;
    public const int TtlMinutes = 15;
    public const int MinSecondsBetweenSends = 60;
    public const int MaxCodesPerHour = 5;
    public const int MaxFailedVerifyAttempts = 5;

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex EightDigitCodeRegex();

    public static bool IsValidCodeFormat(string? code) =>
        !string.IsNullOrWhiteSpace(code) && EightDigitCodeRegex().IsMatch(code.Trim());

    public static string GenerateNumericCode()
    {
        var value = RandomNumberGenerator.GetInt32(10_000_000, 100_000_000);
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static bool TryNormalizeEmail(string? raw, out string email, out string? error)
    {
        email = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Valid email is required";
            return false;
        }

        email = raw.Trim().ToLowerInvariant();
        if (!email.Contains('@') || email.Length > 320)
        {
            error = "Valid email is required";
            return false;
        }

        return true;
    }
}
