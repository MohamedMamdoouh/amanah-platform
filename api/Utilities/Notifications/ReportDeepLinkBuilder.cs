using Amanah.Api.Data.Entities;

namespace Amanah.Api.Utilities.Notifications;

public static class ReportDeepLinkBuilder
{
    public static string ForPublicReport(Report report) => report.Type switch
    {
        ReportType.Lost => $"/lost/{report.Id}",
        ReportType.Found => $"/found/{report.Id}",
        _ => $"/reports/{report.Id}",
    };

    public static string ForMyReport(Guid reportId) => $"/my/reports/{reportId}";
}
