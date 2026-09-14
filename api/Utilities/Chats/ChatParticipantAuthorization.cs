using Amanah.Api.Data.Entities;

namespace Amanah.Api.Utilities.Chats;

public static class ChatParticipantAuthorization
{
    public static bool IsParticipant(ChatThread thread, Guid userId) =>
        thread.Claim.Report.ReporterId == userId || thread.Claim.ClaimantId == userId;

    public static bool IsParticipant(ChatAttachment attachment, Guid userId) =>
        IsParticipant(attachment.ChatThread, userId);
}
