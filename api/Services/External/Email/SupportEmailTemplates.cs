namespace Amanah.Api.Services.External.Email;

internal static class SupportEmailTemplates
{
    public static string BuildSubject(string displayName) =>
        $"أمانة: رسالة دعم من {displayName}";

    public static string BuildPlainText(
        string displayName,
        string normalizedReplyEmail,
        string message)
    {
        return
            "أمانة — الدعم والمساعدة\n\n" +
            $"الاسم الظاهر: {displayName}\n" +
            $"البريد للرد: {normalizedReplyEmail}\n\n" +
            "الرسالة:\n" +
            message;
    }

    public static string BuildHtml(
        string displayName,
        string normalizedReplyEmail,
        string message)
    {
        var safeName = EmailLayout.Encode(displayName);
        var safeEmail = EmailLayout.Encode(normalizedReplyEmail);
        var safeMessage = EmailLayout.Encode(message).Replace("\n", "<br>", StringComparison.Ordinal);

        var body = $"""
            <p style="margin:0 0 20px;font-size:16px;color:{EmailDesignTokens.Muted};text-align:right;">
              وصلت رسالة جديدة من صفحة الدعم في المنصة.
            </p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;background:{EmailDesignTokens.AccentSoft};border:1px solid {EmailDesignTokens.Border};border-radius:12px;">
              <tr>
                <td style="padding:20px;text-align:right;">
                  <p style="margin:0 0 12px;font-size:13px;font-weight:600;color:{EmailDesignTokens.Muted};">بيانات المرسل</p>
                  <p style="margin:0 0 12px;font-size:15px;color:{EmailDesignTokens.Ink};">
                    <span style="color:{EmailDesignTokens.Muted};">الاسم الظاهر:</span>
                    <strong style="font-weight:700;">{safeName}</strong>
                  </p>
                  <p style="margin:0;font-size:15px;color:{EmailDesignTokens.Ink};">
                    <span style="color:{EmailDesignTokens.Muted};">البريد للرد:</span>
                    <a href="mailto:{safeEmail}" style="color:{EmailDesignTokens.Primary};word-break:break-all;">{safeEmail}</a>
                  </p>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 12px;font-size:13px;font-weight:600;color:{EmailDesignTokens.Muted};text-align:right;">الرسالة</p>
            <p style="margin:0;font-size:15px;color:{EmailDesignTokens.Ink};text-align:right;white-space:pre-wrap;">{safeMessage}</p>
            """;

        return EmailLayout.BuildDocument(
            title: "رسالة دعم — أمانة",
            eyebrow: "أمانة",
            headline: "رسالة دعم جديدة",
            bodyHtml: body,
            footerHtml: "يمكنك الرد مباشرة على هذا البريد؛ سيتم توجيه الرد إلى بريد المرسل.");
    }
}
