using Amanah.Api.Utilities.Common;

namespace Amanah.Api.Tests.Utilities;

public class ArabicNormalizerTests
{
    [Fact]
    public void NormalizeForSearch_treats_alef_variants_as_equivalent()
    {
        var withHamza = ArabicNormalizer.NormalizeForSearch("Ahmed");
        var bareAlef = ArabicNormalizer.NormalizeForSearch("احمد");

        Assert.Equal(bareAlef, withHamza);
        Assert.Equal("احمد", withHamza);
    }

    [Fact]
    public void NormalizeForSearch_treats_taa_marbuta_and_haa_as_equivalent()
    {
        var withTaaMarbuta = ArabicNormalizer.NormalizeForSearch("مدرسة");
        var withHaa = ArabicNormalizer.NormalizeForSearch("مدرسه");

        Assert.Equal(withHaa, withTaaMarbuta);
        Assert.Equal("مدرسه", withTaaMarbuta);
    }

    [Fact]
    public void NormalizeForSearch_strips_tatweel()
    {
        Assert.Equal("محمد", ArabicNormalizer.NormalizeForSearch("مـحـمد"));
    }

    [Fact]
    public void NormalizeForSearch_strips_diacritics()
    {
        Assert.Equal("كلمه", ArabicNormalizer.NormalizeForSearch("كَلِمَة"));
    }

    [Fact]
    public void BuildSearchTerms_splits_normalized_query_into_non_empty_terms()
    {
        var terms = ArabicNormalizer.BuildSearchTerms("  كلمة   ثانية ");

        Assert.Equal(["كلمه", "ثانيه"], terms);
    }
}
