namespace Amanah.Api.Data.Entities;

public class User : IEntity
{
    public Guid Id { get; set; }

    public string? NormalizedPhone { get; set; }

    public string? NormalizedEmail { get; set; }

    public required string PasswordHash { get; set; }

    public string? DisplayName { get; set; }

    public UserRole Role { get; set; }

    public bool IsBanned { get; set; }

    public string? BanReason { get; set; }

    public DateTimeOffset? BannedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeactivatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
