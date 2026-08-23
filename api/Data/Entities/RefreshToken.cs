namespace Amanah.Api.Data.Entities;

public class RefreshToken : IEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
