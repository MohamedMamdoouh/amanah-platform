namespace Amanah.Api.Services.Auth;

public readonly record struct AuthIdentifier(
    AuthIdentifierChannel Channel,
    string NormalizedValue);

public static class AuthIdentifierNormalizer
{
    public static bool TryParseChannel(string? channel, out AuthIdentifierChannel parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        switch (channel.Trim().ToLowerInvariant())
        {
            case "phone":
                parsed = AuthIdentifierChannel.Phone;
                return true;
            case "email":
                parsed = AuthIdentifierChannel.Email;
                return true;
            default:
                return false;
        }
    }

    public static bool TryResolve(
        string? channel,
        string? identifier,
        out AuthIdentifier authIdentifier)
    {
        authIdentifier = default;
        return TryParseChannel(channel, out var parsedChannel)
            && TryNormalize(identifier ?? string.Empty, parsedChannel, out authIdentifier);
    }

    public static bool TryNormalize(
        string input,
        AuthIdentifierChannel channel,
        out AuthIdentifier identifier)
    {
        identifier = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        if (channel == AuthIdentifierChannel.Email)
        {
            if (!EmailNormalizer.TryNormalize(trimmed, out var normalizedEmail))
            {
                return false;
            }

            identifier = new AuthIdentifier(AuthIdentifierChannel.Email, normalizedEmail);
            return true;
        }

        if (channel != AuthIdentifierChannel.Phone)
        {
            return false;
        }

        if (!PhoneNormalizer.TryNormalize(trimmed, out var normalizedPhone))
        {
            return false;
        }

        identifier = new AuthIdentifier(AuthIdentifierChannel.Phone, normalizedPhone);
        return true;
    }
}
