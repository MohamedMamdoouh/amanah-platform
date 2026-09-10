using Amanah.Api.Utilities.Common;
using Amanah.Api.Utilities.Reports;

namespace Amanah.Api.Utilities.Claims;

public static class ClaimContentValidator
{
    public const string FieldName = "submittedAnswer";

    public const int MinLength = 10;

    public const int MaxLength = 500;

    public static string NormalizeAnswer(string? rawAnswer) => TextNormalizer.Normalize(rawAnswer);

    public static Dictionary<string, string[]>? Validate(string? rawAnswer)
    {
        var normalized = NormalizeAnswer(rawAnswer);
        var messages = new List<string>();

        if (normalized.Length < MinLength)
        {
            messages.Add($"Answer must be at least {MinLength} characters.");
        }

        if (normalized.Length > MaxLength)
        {
            messages.Add($"Answer cannot exceed {MaxLength} characters.");
        }

        if (ContactInfoDetector.ContainsContactInfo(normalized))
        {
            messages.Add(ContactInfoDetector.ContactInfoMessage);
        }

        if (messages.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, string[]>
        {
            [FieldName] = messages.ToArray(),
        };
    }
}
