using Amanah.Api.Services.Auth;

namespace Amanah.Api.Data.Entities;

public class OtpCode : IEntity
{
    public Guid Id { get; set; }

    public required string Destination { get; set; }

    public AuthIdentifierChannel Channel { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
