namespace Amanah.Contracts.Errors;

public record ApiError(
    string Code,
    string Message,
    Dictionary<string, string[]>? Errors = null);

public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string InvalidPhone = "auth.invalid_phone";
    public const string CaptchaFailed = "auth.captcha_failed";
    public const string InvalidOtp = "auth.invalid_otp";
    public const string OtpExpired = "auth.otp_expired";
    public const string OtpVoid = "auth.otp_void";
    public const string HandoffTokenInvalid = "auth.handoff_token_invalid";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string AccountExists = "auth.account_exists";
    public const string TokenExpired = "auth.token_expired";
    public const string RefreshInvalid = "auth.refresh_invalid";
    public const string Banned = "auth.banned";
    public const string Unauthorized = "auth.unauthorized";
    public const string Forbidden = "auth.forbidden";
    public const string NotFound = "resource.not_found";
    public const string Conflict = "resource.conflict";
    public const string OtpCooldown = "otp.cooldown";
    public const string OtpHourlyLimit = "otp.hourly_limit";
    public const string OtpDailyLimit = "otp.daily_limit";
    public const string RateLimitExceeded = "rate_limit.exceeded";
    public const string SmsUnavailable = "service.sms_unavailable";
    public const string InternalError = "internal.error";

    public const string FieldPhoneRequired = "field.phone.required";
    public const string FieldPhoneInvalid = "field.phone.invalid";
    public const string FieldDisplayNameRequired = "field.display_name.required";
    public const string FieldDisplayNameInvalid = "field.display_name.invalid";
    public const string FieldAcceptTermsRequired = "field.accept_terms.required";
    public const string FieldSignupTokenRequired = "field.signup_token.required";
    public const string FieldCaptchaTokenRequired = "field.captcha_token.required";
    public const string FieldPasswordRequired = "field.password.required";
    public const string FieldPasswordTooShort = "field.password.too_short";
    public const string FieldPasswordInvalid = "field.password.invalid";
    public const string FieldResetTokenRequired = "field.reset_token.required";
    public const string FieldOtpPurposeRequired = "field.otp_purpose.required";
    public const string FieldOtpPurposeInvalid = "field.otp_purpose.invalid";
    public const string FieldRefreshTokenRequired = "field.refresh_token.required";
    public const string FieldOtpCodeRequired = "field.otp_code.required";
    public const string FieldOtpCodeInvalid = "field.otp_code.invalid";

    public const string ReportDailyQuota = "report.daily_quota";
    public const string ReportOpenCap = "report.open_cap";
    public const string ReportContactInfo = "report.contact_info";

    public const string UploadInvalidFormat = "upload.invalid_format";
    public const string UploadTooLarge = "upload.too_large";
    public const string UploadStorageFailed = "upload.storage_failed";
}
