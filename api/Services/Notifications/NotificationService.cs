using Amanah.Api.Data;
using Amanah.Api.Models.Errors;
using Amanah.Contracts.Responses.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Notifications;

public sealed class NotificationService(AppDbContext dbContext, TimeProvider timeProvider)
{
    private const int ListLimit = 50;

    public async Task<Result<NotificationListResponse>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(ListLimit)
            .ToListAsync(cancellationToken);

        return new NotificationListResponse
        {
            Items = notifications.Select(ToItem).ToList(),
        };
    }

    public async Task<Result<NotificationUnreadCountResponse>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var count = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.UserId == userId && !notification.IsRead,
                cancellationToken);

        return new NotificationUnreadCountResponse { Count = count };
    }

    public async Task<Result> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                item => item.Id == notificationId && item.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return ResultError.NotFound("Notification not found.");
        }

        if (notification.IsRead)
        {
            return Result.Ok();
        }

        notification.IsRead = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var unread = await dbContext.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return Result.Ok();
        }

        var now = timeProvider.GetUtcNow();
        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static NotificationItemResponse ToItem(Data.Entities.Notification notification)
    {
        var payload = NotificationPayload.FromJson(notification.PayloadJson);

        return new NotificationItemResponse
        {
            Id = notification.Id,
            Payload = new NotificationPayloadResponse
            {
                Type = payload.Type,
                CreatedAt = payload.CreatedAt,
                DeepLink = payload.DeepLink,
                ReportId = payload.ReportId,
                ReasonCode = payload.ReasonCode,
                Note = payload.Note,
            },
            IsRead = notification.IsRead,
        };
    }
}
