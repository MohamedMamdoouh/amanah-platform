namespace Amanah.Api.Services.External.Email;

internal static class AdminAlertEmailTemplates
{
  public static string BuildSubject(string reportType) =>
      string.Equals(reportType, "found", StringComparison.OrdinalIgnoreCase)
          ? "أمانة: بلاغ «عثر عليه» جديد بانتظار المراجعة"
          : "أمانة: بلاغ «مفقود» جديد بانتظار المراجعة";

  public static string BuildPlainText(string reportType, string categoryCode, string reviewLink)
  {
    var typeLabel = GetTypeLabel(reportType);
    return
        $"أمانة — قائمة المراجعة\n\n" +
        $"بلاغ جديد ({typeLabel}) ({categoryCode}) بانتظار مراجعتك.\n\n" +
        $"المراجعة: {reviewLink}\n\n" +
        "هذا التنبيه لا يتضمن بيانات اتصال المبلغ أو محتوى البلاغ الخاص.";
  }

  public static string BuildHtml(string reportType, string categoryCode, string reviewLink)
  {
    var badge = BuildTypeBadge(reportType);
    var safeCategory = EmailLayout.Encode(categoryCode);
    var safeLink = EmailLayout.Encode(reviewLink);

    var body = $"""
            <p style="margin:0 0 20px;font-size:16px;color:{EmailDesignTokens.Muted};text-align:right;">
              وصل بلاغ جديد إلى قائمة المراجعة وهو جاهز للاطلاع.
            </p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;background:{EmailDesignTokens.AccentSoft};border:1px solid {EmailDesignTokens.Border};border-radius:12px;">
              <tr>
                <td style="padding:20px;text-align:right;">
                  <p style="margin:0 0 12px;font-size:13px;font-weight:600;color:{EmailDesignTokens.Muted};text-transform:uppercase;letter-spacing:0.04em;">تفاصيل البلاغ</p>
                  <p style="margin:0 0 12px;">{badge}</p>
                  <p style="margin:0;font-size:15px;color:{EmailDesignTokens.Ink};">
                    <span style="color:{EmailDesignTokens.Muted};">التصنيف:</span>
                    <strong style="font-weight:700;">{safeCategory}</strong>
                  </p>
                </td>
              </tr>
            </table>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
              <tr>
                <td style="text-align:right;">
                  <a href="{safeLink}" style="display:inline-block;padding:14px 22px;background:{EmailDesignTokens.Primary};color:#ffffff;text-decoration:none;font-size:15px;font-weight:700;border-radius:12px;border:1px solid {EmailDesignTokens.Primary};">
                    فتح في قائمة المراجعة
                  </a>
                </td>
              </tr>
            </table>
            <p style="margin:0;font-size:13px;color:{EmailDesignTokens.Muted};text-align:right;">
              أو انسخ هذا الرابط:<br />
              <a href="{safeLink}" style="color:{EmailDesignTokens.Primary};word-break:break-all;">{safeLink}</a>
            </p>
            """;

    return EmailLayout.BuildDocument(
        title: "تنبيه مراجعة — أمانة",
        headline: "بلاغ جديد بانتظار المراجعة",
        bodyHtml: body,
        footerHtml: "يستبعد هذا التنبيه عمدًا أرقام هواتف المبلغين وتفاصيل التحقق المخفية والصور.",
        eyebrow: "أمانة");
  }

  private static string GetTypeLabel(string reportType) =>
      string.Equals(reportType, "found", StringComparison.OrdinalIgnoreCase)
          ? "عثر عليه"
          : "مفقود";

  private static string BuildTypeBadge(string reportType)
  {
    var isFound = string.Equals(reportType, "found", StringComparison.OrdinalIgnoreCase);
    var background = isFound ? EmailDesignTokens.InfoBg : EmailDesignTokens.ErrorBg;
    var color = isFound ? EmailDesignTokens.Info : EmailDesignTokens.Error;
    var label = isFound ? "عثر عليه" : "مفقود";

    return $"""
            <span style="display:inline-block;padding:4px 12px;border-radius:999px;font-size:12px;font-weight:700;line-height:1.3;color:{color};background:{background};">
              {label}
            </span>
            """;
  }
}
