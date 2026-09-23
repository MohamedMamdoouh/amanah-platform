namespace Amanah.Api.Services.External.Email;

internal static class OtpEmailTemplates
{
  public static string BuildSubject() => "رمز التأكيد — أمانة";

  public static string BuildPlainText(string code, int lifetimeMinutes) =>
      $"رمز التأكيد في أمانة: {code}\n\n" +
      $"صالح لمدة {lifetimeMinutes} دقيقة. لا تشارك هذا الرمز مع أي شخص.";

  public static string BuildHtml(string code, int lifetimeMinutes)
  {
    var safeCode = EmailLayout.Encode(code);

    var body = $"""
            <p style="margin:0 0 20px;font-size:16px;color:{EmailDesignTokens.Muted};text-align:right;">
              استخدم الرمز التالي لتأكيد هويتك في أمانة.
            </p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;background:{EmailDesignTokens.AccentSoft};border:1px solid {EmailDesignTokens.Border};border-radius:12px;">
              <tr>
                <td style="padding:24px;text-align:center;">
                  <p style="margin:0;font-size:32px;font-weight:800;letter-spacing:0.2em;font-variant-numeric:tabular-nums;color:{EmailDesignTokens.Ink};">{safeCode}</p>
                </td>
              </tr>
            </table>
            <p style="margin:0;font-size:15px;color:{EmailDesignTokens.Muted};text-align:right;">
              ينتهي صلاحية هذا الرمز خلال <strong style="color:{EmailDesignTokens.Ink};">{lifetimeMinutes}</strong> دقائق.
              إذا لم تطلب هذا الرمز، يمكنك تجاهل هذه الرسالة.
            </p>
            """;

    return EmailLayout.BuildDocument(
        title: BuildSubject(),
        eyebrow: "أمانة",
        headline: "رمز التأكيد",
        bodyHtml: body,
        footerHtml: "لا تشارك رمز التأكيد مع أي شخص. فريق أمانة لن يطلب منك هذا الرمز عبر الهاتف أو رسائل أخرى.");
  }
}
