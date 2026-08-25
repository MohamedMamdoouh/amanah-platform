using System.Text;

namespace Amanah.Api.Services.Auth;

public static class OtpCodeNormalizer
{
    public static bool TryNormalize(string? input, out string normalizedCode)
    {
        normalizedCode = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var builder = new StringBuilder(6);

        foreach (var character in input.Trim())
        {
            if (character is >= '٠' and <= '٩')
            {
                builder.Append((char)(character - '٠' + '0'));
                continue;
            }

            if (char.IsDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return false;
        }

        if (builder.Length != 6)
        {
            return false;
        }

        normalizedCode = builder.ToString();
        return true;
    }
}
