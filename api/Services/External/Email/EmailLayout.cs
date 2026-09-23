using System.Net;

namespace Amanah.Api.Services.External.Email;

internal static class EmailLayout
{
  public static string Encode(string value) => WebUtility.HtmlEncode(value);

  public static string BuildDocument(
      string title,
      string eyebrow,
      string headline,
      string bodyHtml,
      string footerHtml)
  {
    var safeTitle = Encode(title);
    var safeEyebrow = Encode(eyebrow);
    var safeHeadline = Encode(headline);
    var safeFooter = Encode(footerHtml);

    return $"""
            <!DOCTYPE html>
            <html lang="ar" dir="rtl">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{safeTitle}</title>
              <link href="{EmailDesignTokens.GoogleFontsHref}" rel="stylesheet" />
            </head>
            <body style="margin:0;padding:0;background:{EmailDesignTokens.Paper};font-family:{EmailDesignTokens.FontStack};color:{EmailDesignTokens.Ink};line-height:1.6;direction:rtl;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{EmailDesignTokens.Paper};padding:32px 16px;direction:rtl;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:{EmailDesignTokens.Surface};border:1px solid {EmailDesignTokens.Border};border-radius:16px;overflow:hidden;box-shadow:0 8px 32px rgba(26,40,43,0.05);direction:rtl;">
                      <tr>
                        <td style="padding:28px 28px 20px;background:{EmailDesignTokens.Primary};color:#ffffff;text-align:right;">
                          <p style="margin:0 0 8px;font-size:12px;font-weight:600;letter-spacing:0.08em;text-transform:uppercase;color:{EmailDesignTokens.Warm};">{safeEyebrow}</p>
                          <h1 style="margin:0;font-size:24px;line-height:1.25;font-weight:800;text-align:right;">{safeHeadline}</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px;text-align:right;direction:rtl;">
                          {bodyHtml}
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:18px 28px;border-top:1px solid {EmailDesignTokens.Border};background:{EmailDesignTokens.Paper};text-align:right;">
                          <p style="margin:0;font-size:12px;color:{EmailDesignTokens.Muted};">{safeFooter}</p>
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
}
