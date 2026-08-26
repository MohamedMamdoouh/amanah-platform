namespace Amanah.Api.Data.Entities;

public class ChatThread : IEntity
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }

    public Claim Claim { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadOnlyAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
