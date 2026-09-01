using Amanah.Api.Data.Entities;
using Amanah.Api.Utilities.Reports;

namespace Amanah.Api.Tests.Utilities;

public class ContactInfoDetectorTests
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("visit www.example.com")]
    [InlineData("facebook.com/my-page")]
    [InlineData("instagram.com/user")]
    [InlineData("t.me/username")]
    [InlineData("telegram.me/username")]
    [InlineData("wa.me/201012345678")]
    [InlineData("whatsapp.com/channel")]
    [InlineData("01012345678")]
    [InlineData("٠١٠١٢٣٤٥٦٧٨")]
    [InlineData("01 0123-4567.8")]
    public void ContainsContactInfo_returns_true_for_blocked_patterns(string text)
    {
        Assert.True(ContactInfoDetector.ContainsContactInfo(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lost black wallet near station")]
    [InlineData("123456789")]
    [InlineData("serial 12345")]
    public void ContainsContactInfo_returns_false_for_allowed_text(string? text)
    {
        Assert.False(ContactInfoDetector.ContainsContactInfo(text));
    }

    [Fact]
    public void ContainsContactInfo_is_case_insensitive_for_urls_and_domains()
    {
        Assert.True(ContactInfoDetector.ContainsContactInfo("HTTPS://Example.COM"));
        Assert.True(ContactInfoDetector.ContainsContactInfo("WWW.Facebook.COM/page"));
    }
}

public class ReportDateValidatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 31);

    [Fact]
    public void ValidateDateLostOrFound_accepts_today()
    {
        Assert.Null(ReportDateValidator.ValidateDateLostOrFound(Today, Today));
    }

    [Fact]
    public void ValidateDateLostOrFound_rejects_future_date()
    {
        var error = ReportDateValidator.ValidateDateLostOrFound(Today.AddDays(1), Today);

        Assert.NotNull(error);
        Assert.Contains("future", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateDateLostOrFound_accepts_date_exactly_twelve_months_ago()
    {
        Assert.Null(ReportDateValidator.ValidateDateLostOrFound(Today.AddMonths(-12), Today));
    }

    [Fact]
    public void ValidateDateLostOrFound_rejects_date_older_than_twelve_months()
    {
        var error = ReportDateValidator.ValidateDateLostOrFound(
            Today.AddMonths(-12).AddDays(-1),
            Today);

        Assert.NotNull(error);
        Assert.Contains("12 months", error, StringComparison.OrdinalIgnoreCase);
    }
}

public class CategoryFieldValidatorTests
{
    [Fact]
    public void Validate_returns_error_for_missing_required_field()
    {
        var definitions = CreatePhoneDefinitions();
        var errors = CategoryFieldValidator.Validate(definitions, new Dictionary<string, string>());

        Assert.Contains("brand_model", errors.Keys);
        Assert.Contains("colour", errors.Keys);
    }

    [Fact]
    public void Validate_returns_error_for_text_too_short()
    {
        var definitions = CreatePhoneDefinitions();
        var errors = CategoryFieldValidator.Validate(definitions, new Dictionary<string, string>
        {
            ["brand_model"] = "a",
            ["colour"] = "black",
        });

        Assert.Contains("brand_model", errors.Keys);
        Assert.DoesNotContain("colour", errors.Keys);
    }

    [Fact]
    public void Validate_returns_error_for_unknown_field_key()
    {
        var definitions = CreatePhoneDefinitions();
        var errors = CategoryFieldValidator.Validate(definitions, new Dictionary<string, string>
        {
            ["brand_model"] = "iPhone 14",
            ["colour"] = "black",
            ["unknown_field"] = "value",
        });

        Assert.Contains("unknown_field", errors.Keys);
    }

    [Fact]
    public void Validate_returns_error_for_integer_out_of_range()
    {
        var definitions = new List<CategoryFieldDefinition>
        {
            new()
            {
                FieldKey = "key_count",
                Type = CategoryFieldType.Integer,
                Required = true,
                MinInt = 1,
                MaxInt = 20,
            },
        };

        var errors = CategoryFieldValidator.Validate(definitions, new Dictionary<string, string>
        {
            ["key_count"] = "25",
        });

        Assert.Contains("key_count", errors.Keys);
    }

    [Fact]
    public void Validate_rejects_first_name_on_document_with_digits()
    {
        var definitions = new List<CategoryFieldDefinition>
        {
            new()
            {
                FieldKey = "first_name_on_document",
                Type = CategoryFieldType.Text,
                Required = true,
                MinLength = 2,
                MaxLength = 40,
                TextFormat = CategoryTextFormat.LettersAndSpaces,
            },
        };

        var errors = CategoryFieldValidator.Validate(definitions, new Dictionary<string, string>
        {
            ["first_name_on_document"] = "Ahmed1",
        });

        Assert.Contains("first_name_on_document", errors.Keys);
    }

    [Fact]
    public void Validate_accepts_valid_phone_category_fields()
    {
        var definitions = CreatePhoneDefinitions();
        var errors = CategoryFieldValidator.Validate(definitions, new Dictionary<string, string>
        {
            ["brand_model"] = "iPhone 14",
            ["colour"] = "black",
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_accepts_digits_when_text_format_is_not_set()
    {
        var definitions = new List<CategoryFieldDefinition>
        {
            new()
            {
                FieldKey = "model_number",
                Type = CategoryFieldType.Text,
                Required = true,
                MinLength = 2,
                MaxLength = 80,
            },
        };

        var errors = CategoryFieldValidator.Validate(definitions, new Dictionary<string, string>
        {
            ["model_number"] = "ABC123",
        });

        Assert.Empty(errors);
    }

    private static List<CategoryFieldDefinition> CreatePhoneDefinitions() =>
    [
        new()
        {
            FieldKey = "brand_model",
            Type = CategoryFieldType.Text,
            Required = true,
            MinLength = 2,
            MaxLength = 80,
        },
        new()
        {
            FieldKey = "colour",
            Type = CategoryFieldType.Text,
            Required = true,
            MinLength = 2,
            MaxLength = 80,
        },
    ];
}

public class SearchTextBuilderTests
{
    [Fact]
    public void Build_joins_title_description_category_fields_and_area()
    {
        var result = SearchTextBuilder.Build(
            "Lost Phone",
            "Black iPhone near station",
            "Nasr City",
            ["iPhone 14", "black"]);

        Assert.Contains("lost phone", result, StringComparison.Ordinal);
        Assert.Contains("iphone", result, StringComparison.Ordinal);
        Assert.Contains("nasr city", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_applies_arabic_normalization()
    {
        var result = SearchTextBuilder.Build(
            "مدرسة",
            string.Empty,
            null,
            []);

        Assert.Equal("مدرسه", result);
    }

    [Fact]
    public void Build_returns_empty_string_when_all_inputs_empty()
    {
        Assert.Equal(string.Empty, SearchTextBuilder.Build(string.Empty, string.Empty, null, []));
    }
}
