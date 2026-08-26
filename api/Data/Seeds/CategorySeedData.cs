namespace Amanah.Api.Data.Seeds;

internal static class CategorySeedData
{
    internal sealed record FieldSeed(
        string FieldKey,
        string Type,
        bool Required,
        int SortOrder,
        int? MinLength = null,
        int? MaxLength = null,
        int? MinInt = null,
        int? MaxInt = null);

    internal sealed record CategorySeed(
        string Code,
        int SortOrder,
        bool PhotosPrivate,
        IReadOnlyList<FieldSeed> Fields);

    internal static readonly IReadOnlyList<CategorySeed> Categories = [
        new("phones", 1, false, [
            new FieldSeed("brand_model", "Text", true, 1, 2, 80),
            new FieldSeed("colour", "Text", true, 2, 2, 80),
        ]),
        new("documents-ids", 2, true, [
            new FieldSeed("document_type", "Text", true, 1, 2, 80),
            new FieldSeed("first_name_on_document", "Text", true, 2, 2, 40),
        ]),
        new("wallets", 3, false, [
            new FieldSeed("wallet_type", "Text", true, 1, 2, 80),
            new FieldSeed("colour", "Text", true, 2, 2, 80),
        ]),
        new("keys", 4, false, [
            new FieldSeed("key_type", "Text", true, 1, 2, 80),
            new FieldSeed("key_count", "Integer", true, 2, null, null, 1, 20),
        ]),
        new("bags", 5, false, [
            new FieldSeed("bag_type", "Text", true, 1, 2, 80),
            new FieldSeed("colour", "Text", true, 2, 2, 80),
        ]),
        new("electronics", 6, false, [
            new FieldSeed("device_type", "Text", true, 1, 2, 80),
            new FieldSeed("brand_model", "Text", true, 2, 2, 80),
        ]),
        new("accessories", 7, false, [
            new FieldSeed("accessory_type", "Text", true, 1, 2, 80),
        ]),
        new("other", 8, false, [
            new FieldSeed("item_type", "Text", true, 1, 2, 80),
        ]),
    ];
}
