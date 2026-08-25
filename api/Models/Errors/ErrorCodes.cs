namespace Amanah.Api.Models.Errors;

public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string InvalidPhone = "auth.invalid_phone";
    public const string CaptchaFailed = "auth.captcha_failed";
    public const string InvalidOtp = "auth.invalid_otp";
    public const string OtpExpired = "auth.otp_expired";
    public const string OtpVoid = "auth.otp_void";
    public const string HandoffTokenInvalid = "auth.handoff_token_invalid";
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
}
