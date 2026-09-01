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
        IReadOnlyList<CategoryFieldDefinition> definitions,
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
