namespace Amanah.Contracts.Requests.Admin;

public static class AdminUserSearchBy
{
    public const string Name = "name";

    public const string Phone = "phone";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Name,
        Phone,
    };
}
