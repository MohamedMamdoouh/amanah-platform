namespace Amanah.Contracts.Chats;

public static class ChatHubRoutes
{
    public const string Path = "/hubs/chat";
}

public static class ChatHubMethods
{
    public const string JoinThread = nameof(JoinThread);

    public const string LeaveThread = nameof(LeaveThread);

    public const string SendMessage = nameof(SendMessage);
}

public static class ChatHubEvents
{
    public const string MessageReceived = nameof(MessageReceived);

    public const string ThreadReadOnly = nameof(ThreadReadOnly);
}

public static class ChatHubGroups
{
    public const string ThreadPrefix = "thread:";

    public static string ForThread(Guid threadId) => $"{ThreadPrefix}{threadId}";
}
