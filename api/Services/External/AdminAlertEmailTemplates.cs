using System.Net;

namespace Amanah.Api.Services.External;

internal static class AdminAlertEmailTemplates
{
    private const string ColorBg = "#f7f4ef";
    private const string ColorSurface = "#ffffff";
    private const string ColorText = "#1a282b";
    private const string ColorTextMuted = "#5c6769";
    private const string ColorPrimary = "#086060";
    private const string ColorWarm = "#e0b890";
    private const string ColorBorder = "#d5dfdb";
    private const string ColorAccentSoft = "#e8f2ed";

    public static string BuildSubject(string reportType) =>
        string.Equals(reportType, "found", StringComparison.OrdinalIgnoreCase)
            ? "Amanah: new found-item report pending moderation"
            : "Amanah: new lost-item report pending moderation";

    public static string BuildPlainText(string reportType, string categoryCode, string reviewLink)
    {
        var typeLabel = GetTypeLabel(reportType);
        return
            $"Amanah — moderation queue\n\n" +
            $"A new {typeLabel} report ({categoryCode}) is waiting for review.\n\n" +
            $"Review: {reviewLink}\n\n" +
            "This alert contains no reporter contact details or private report content.";
    }

    public static string BuildHtml(string reportType, string categoryCode, string reviewLink)
    {
        var badge = BuildTypeBadge(reportType);
        var safeCategory = WebUtility.HtmlEncode(categoryCode);
        var safeLink = WebUtility.HtmlEncode(reviewLink);

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Amanah moderation alert</title>
            </head>
            <body style="margin:0;padding:0;background:{ColorBg};font-family:'Segoe UI',Tahoma,Arial,sans-serif;color:{ColorText};line-height:1.6;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{ColorBg};padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:{ColorSurface};border:1px solid {ColorBorder};border-radius:16px;overflow:hidden;box-shadow:0 8px 32px rgba(26,40,43,0.05);">
                      <tr>
                        <td style="padding:28px 28px 20px;background:{ColorPrimary};color:#ffffff;">
                          <p style="margin:0 0 8px;font-size:12px;font-weight:600;letter-spacing:0.08em;text-transform:uppercase;color:{ColorWarm};">Amanah</p>
                          <h1 style="margin:0;font-size:24px;line-height:1.25;font-weight:800;">New report pending review</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;">
                          <p style="margin:0 0 20px;font-size:16px;color:{ColorTextMuted};">
                            A submission has entered the moderation queue and is ready for your review.
                          </p>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;background:{ColorAccentSoft};border:1px solid {ColorBorder};border-radius:12px;">
                            <tr>
                              <td style="padding:20px;">
                                <p style="margin:0 0 12px;font-size:13px;font-weight:600;color:{ColorTextMuted};text-transform:uppercase;letter-spacing:0.04em;">Report details</p>
                                <p style="margin:0 0 12px;">{badge}</p>
                                <p style="margin:0;font-size:15px;color:{ColorText};">
                                  <span style="color:{ColorTextMuted};">Category:</span>
                                  <strong style="font-weight:700;">{safeCategory}</strong>
                                </p>
                              </td>
                            </tr>
                          </table>
                          <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
                            <tr>
                              <td>
                                <a href="{safeLink}" style="display:inline-block;padding:14px 22px;background:{ColorPrimary};color:#ffffff;text-decoration:none;font-size:15px;font-weight:700;border-radius:12px;border:1px solid {ColorPrimary};">
                                  Open in moderation queue
                                </a>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:0;font-size:13px;color:{ColorTextMuted};">
                            Or copy this link:<br />
                            <a href="{safeLink}" style="color:{ColorPrimary};word-break:break-all;">{safeLink}</a>
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:18px 28px;border-top:1px solid {ColorBorder};background:{ColorBg};">
                          <p style="margin:0;font-size:12px;color:{ColorTextMuted};">
                            This alert intentionally excludes reporter phone numbers, hidden verification details, and photos.
                          </p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string GetTypeLabel(string reportType) =>
        string.Equals(reportType, "found", StringComparison.OrdinalIgnoreCase)
            ? "found-item"
            : "lost-item";

    private static string BuildTypeBadge(string reportType)
    {
        var isFound = string.Equals(reportType, "found", StringComparison.OrdinalIgnoreCase);
        var background = isFound ? "#e8f2f8" : "#fdecec";
        var color = isFound ? "#1a4d6d" : "#8b1e1e";
        var label = isFound ? "Found item" : "Lost item";

        return $"""
            <span style="display:inline-block;padding:4px 12px;border-radius:999px;font-size:12px;font-weight:700;line-height:1.3;color:{color};background:{background};">
              {label}
            </span>
            """;
    }
}
