using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Data.Extensions;

public static class ReportQueryExtensions
{
    public static IQueryable<Report> WithCategoryInclude(this IQueryable<Report> query) =>
        query.Include(report => report.Category);

    public static IQueryable<Report> WithBrowseSummaryIncludes(this IQueryable<Report> query) =>
        query
            .Include(report => report.Category)
            .Include(report => report.Governorate)
            .Include(report => report.Reporter)
            .Include(report => report.Photos);

    public static IQueryable<Report> WithPublicDetailIncludes(this IQueryable<Report> query) =>
        query
            .WithBrowseSummaryIncludes()
            .Include(report => report.CategoryFields);

    public static IQueryable<Report> WithOwnerSummaryIncludes(this IQueryable<Report> query) =>
        query
            .Include(report => report.Category)
            .Include(report => report.Governorate);

    public static IQueryable<Report> WithOwnerDetailIncludes(this IQueryable<Report> query) =>
        query
            .Include(report => report.Category)
            .Include(report => report.Governorate)
            .Include(report => report.CategoryFields)
            .Include(report => report.Photos);
}
