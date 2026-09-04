using Amanah.Api.Data.Entities;

namespace Amanah.Api.Utilities.Reports;

// Top-level date lost/found validation (Cairo calendar).
public static class ReportDateValidator
{
    // Used as the field key in validation error responses.
    public const string FieldName = "dateLostOrFound";

    public static string? ValidateDateLostOrFound(DateOnly date, DateOnly? todayInCairo = null)
    {
        var today = todayInCairo ?? CairoTime.TodayInCairo();
        var oldestAllowed = today.AddMonths(-12);

        if (date > today)
        {
            return "Date cannot be in the future.";
        }

        if (date < oldestAllowed)
        {
            return "Date cannot be more than 12 months ago.";
        }

        return null;
    }
}

// Category-specific field validation from DB definitions.
public static class CategoryFieldValidator
{
    public static Dictionary<string, string[]> Validate(
        IEnumerable<CategoryFieldDefinition> definitions,
        IReadOnlyDictionary<string, string> submittedValues)
    {
        var errors = new Dictionary<string, string[]>();
        var definitionByKey = definitions.ToDictionary(definition => definition.FieldKey);

        // Reject keys not defined for this category.
        foreach (var (fieldKey, rawValue) in submittedValues)
        {
            if (!definitionByKey.ContainsKey(fieldKey))
            {
                AddError(errors, fieldKey, "Unknown category field.");
            }
        }

        // Validate required, type, and bounds for each defined field.
        foreach (var definition in definitions)
        {
            submittedValues.TryGetValue(definition.FieldKey, out var rawValue);
            var normalized = TextNormalizer.Normalize(rawValue);

            if (definition.Required && normalized.Length == 0)
            {
                AddError(errors, definition.FieldKey, "This field is required.");
                continue;
            }

            if (normalized.Length == 0)
            {
                continue;
            }

            switch (definition.Type)
            {
                case CategoryFieldType.Text:
                    ValidateTextField(definition, normalized, errors);
                    break;
                case CategoryFieldType.Integer:
                    ValidateIntegerField(definition, normalized, errors);
                    break;
            }
        }

        return errors;
    }

    private static void ValidateTextField(
        CategoryFieldDefinition definition,
        string normalized,
        Dictionary<string, string[]> errors)
    {
        if (definition.MinLength is int minLength && normalized.Length < minLength)
        {
            AddError(errors, definition.FieldKey, $"Must be at least {minLength} characters.");
            return;
        }

        if (definition.MaxLength is int maxLength && normalized.Length > maxLength)
        {
            AddError(errors, definition.FieldKey, $"Must be at most {maxLength} characters.");
            return;
        }

        if (definition.TextFormat == CategoryTextFormat.LettersAndSpaces
            && !normalized.All(character => char.IsLetter(character) || character == ' '))
        {
            AddError(errors, definition.FieldKey, "Must contain letters and spaces only.");
        }
    }

    private static void ValidateIntegerField(
        CategoryFieldDefinition definition,
        string normalized,
        Dictionary<string, string[]> errors)
    {
        if (!int.TryParse(normalized, out var value))
        {
            AddError(errors, definition.FieldKey, "Must be a whole number.");
            return;
        }

        if (definition.MinInt is int minInt && value < minInt)
        {
            AddError(errors, definition.FieldKey, $"Must be at least {minInt}.");
            return;
        }

        if (definition.MaxInt is int maxInt && value > maxInt)
        {
            AddError(errors, definition.FieldKey, $"Must be at most {maxInt}.");
        }
    }

    private static void AddError(
        Dictionary<string, string[]> errors,
        string fieldKey,
        string message)
    {
        if (errors.TryGetValue(fieldKey, out var existing))
        {
            errors[fieldKey] = [.. existing, message];
            return;
        }

        errors[fieldKey] = [message];
    }
}

public static class ReportContentValidator
{
    public const string TitleField = "title";
    public const string DescriptionField = "description";
    public const string HiddenDetailField = "hiddenDetail";
    public const string AreaTextField = "areaText";
    public const string RewardAmountField = "rewardAmount";
    public const string HeldLocationField = "heldLocation";

    private const int TitleMinLength = 10;
    private const int TitleMaxLength = 80;
    private const int DescriptionMinLength = 20;
    private const int DescriptionMaxLength = 1000;
    private const int HiddenDetailMinLength = 10;
    private const int HiddenDetailMaxLength = 500;
    private const int AreaTextMaxLength = 120;
    private const int RewardMinAmount = 50;
    private const int RewardMaxAmount = 50_000;
    private const int HeldLocationMaxLength = 120;

    public static Dictionary<string, string[]> Validate(
        bool isFoundReport,
        string title,
        string description,
        string hiddenDetail,
        string? areaText,
        bool hasReward,
        int? rewardAmount,
        string? heldLocation)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateTitle(title, errors);
        ValidateDescription(description, errors);
        ValidateHiddenDetail(hiddenDetail, errors);
        ValidateAreaText(areaText, errors);
        ValidateReward(hasReward, rewardAmount, errors);
        ValidateHeldLocation(isFoundReport, heldLocation, errors);

        return errors;
    }

    private static void ValidateTitle(string title, Dictionary<string, string[]> errors)
    {
        if (title.Length == 0)
        {
            AddError(errors, TitleField, "Title is required.");
            return;
        }

        if (title.Length < TitleMinLength)
        {
            AddError(errors, TitleField, $"Title must be at least {TitleMinLength} characters.");
            return;
        }

        if (title.Length > TitleMaxLength)
        {
            AddError(errors, TitleField, $"Title must be at most {TitleMaxLength} characters.");
        }
    }

    private static void ValidateDescription(string description, Dictionary<string, string[]> errors)
    {
        if (description.Length == 0)
        {
            AddError(errors, DescriptionField, "Description is required.");
            return;
        }

        if (description.Length < DescriptionMinLength)
        {
            AddError(errors, DescriptionField, $"Description must be at least {DescriptionMinLength} characters.");
            return;
        }

        if (description.Length > DescriptionMaxLength)
        {
            AddError(errors, DescriptionField, $"Description must be at most {DescriptionMaxLength} characters.");
        }
    }

    private static void ValidateHiddenDetail(string hiddenDetail, Dictionary<string, string[]> errors)
    {
        if (hiddenDetail.Length == 0)
        {
            AddError(errors, HiddenDetailField, "Hidden verification detail is required.");
            return;
        }

        if (hiddenDetail.Length < HiddenDetailMinLength)
        {
            AddError(errors, HiddenDetailField, $"Hidden verification detail must be at least {HiddenDetailMinLength} characters.");
            return;
        }

        if (hiddenDetail.Length > HiddenDetailMaxLength)
        {
            AddError(errors, HiddenDetailField, $"Hidden verification detail must be at most {HiddenDetailMaxLength} characters.");
        }
    }

    private static void ValidateAreaText(string? areaText, Dictionary<string, string[]> errors)
    {
        if (areaText is not null && areaText.Length > AreaTextMaxLength)
        {
            AddError(errors, AreaTextField, $"Area must be at most {AreaTextMaxLength} characters.");
        }
    }

    private static void ValidateReward(
        bool hasReward,
        int? rewardAmount,
        Dictionary<string, string[]> errors)
    {
        if (hasReward)
        {
            if (rewardAmount is not int amount)
            {
                AddError(errors, RewardAmountField, "Reward amount is required when a reward is offered.");
                return;
            }

            if (amount < RewardMinAmount || amount > RewardMaxAmount)
            {
                AddError(
                    errors,
                    RewardAmountField,
                    $"Reward amount must be between {RewardMinAmount} and {RewardMaxAmount} EGP.");
            }

            return;
        }

        if (rewardAmount is not null)
        {
            AddError(errors, RewardAmountField, "Reward amount must be empty when no reward is offered.");
        }
    }

    private static void ValidateHeldLocation(
        bool isFoundReport,
        string? heldLocation,
        Dictionary<string, string[]> errors)
    {
        if (!isFoundReport)
        {
            if (!string.IsNullOrEmpty(heldLocation))
            {
                AddError(errors, HeldLocationField, "Held location is only allowed for found reports.");
            }

            return;
        }

        if (string.IsNullOrEmpty(heldLocation))
        {
            AddError(errors, HeldLocationField, "Held location is required for found reports.");
            return;
        }

        if (heldLocation.Length > HeldLocationMaxLength)
        {
            AddError(errors, HeldLocationField, $"Held location must be at most {HeldLocationMaxLength} characters.");
        }
    }

    private static void AddError(
        Dictionary<string, string[]> errors,
        string fieldKey,
        string message)
    {
        if (errors.TryGetValue(fieldKey, out var existing))
        {
            errors[fieldKey] = [.. existing, message];
            return;
        }

        errors[fieldKey] = [message];
    }
}

public static class ContactInfoValidator
{
    public static Dictionary<string, string[]> ScanPublicFields(
        string title,
        string description,
        string? areaText,
        string? heldLocation,
        IReadOnlyDictionary<string, string> categoryFieldValues)
    {
        var errors = new Dictionary<string, string[]>();

        ScanField(errors, ReportContentValidator.TitleField, title);
        ScanField(errors, ReportContentValidator.DescriptionField, description);
        ScanField(errors, ReportContentValidator.AreaTextField, areaText);
        ScanField(errors, ReportContentValidator.HeldLocationField, heldLocation);

        foreach (var (fieldKey, value) in categoryFieldValues)
        {
            ScanField(errors, fieldKey, value);
        }

        return errors;
    }

    private static void ScanField(
        Dictionary<string, string[]> errors,
        string fieldKey,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!ContactInfoDetector.ContainsContactInfo(value))
        {
            return;
        }

        errors[fieldKey] = [ContactInfoDetector.ContactInfoMessage];
    }
}
