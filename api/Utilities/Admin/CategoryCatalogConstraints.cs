namespace Amanah.Api.Utilities.Admin;

public static class CategoryCatalogConstraints
{
    public const string CategoryCodePattern = @"^[a-z0-9]+(?:-[a-z0-9]+)*$";

    public const string FieldKeyPattern = @"^[a-z][a-z0-9_]*$";

    public static readonly IReadOnlySet<string> ValidFieldTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text", "integer" };

    public static readonly IReadOnlySet<string> ValidTextFormats =
        new HashSet<string>(StringComparer.Ordinal) { "letters_and_spaces" };

    public static bool IsValidFieldType(string type) =>
        ValidFieldTypes.Contains(type.Trim());

    public static bool IsValidTextFormat(string? textFormat) =>
        textFormat is null || ValidTextFormats.Contains(textFormat.Trim());

    public static bool IsTextType(string type) =>
        type.Trim().Equals("text", StringComparison.OrdinalIgnoreCase);
}
