using PhoneNumbers;

namespace Amanah.Api.Services.Auth;

public static class PhoneNormalizer
{
    private const string EgyptRegion = "EG";

    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            var phoneNumber = PhoneUtil.Parse(input.Trim(), EgyptRegion);

            if (!PhoneUtil.IsValidNumberForRegion(phoneNumber, EgyptRegion))
            {
                return false;
            }

            var numberType = PhoneUtil.GetNumberType(phoneNumber);
            if (numberType is not PhoneNumberType.MOBILE and not PhoneNumberType.FIXED_LINE_OR_MOBILE)
            {
                return false;
            }

            normalized = PhoneUtil.Format(phoneNumber, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }
}
