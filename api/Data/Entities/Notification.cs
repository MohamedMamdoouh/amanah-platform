namespace Amanah.Api.Data.Entities;

public class Notification : IEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public required string Type { get; set; }

    public required string PayloadJson { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
