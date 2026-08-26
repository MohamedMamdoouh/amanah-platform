namespace Amanah.Api.Data.Entities;

public class User : IEntity
{
    public Guid Id { get; set; }

    public required string NormalizedPhone { get; set; }

    public string? DisplayName { get; set; }

    public UserRole Role { get; set; }

    public bool IsBanned { get; set; }

    public string? BanReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
