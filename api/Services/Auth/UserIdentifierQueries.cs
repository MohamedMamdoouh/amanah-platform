using Amanah.Api.Data.Entities;

namespace Amanah.Api.Services.Auth;

public static class UserIdentifierQueries
{
    public static IQueryable<User> ForIdentifier(IQueryable<User> users, AuthIdentifier identifier) =>
        identifier.Channel switch
        {
            AuthIdentifierChannel.Phone => users.Where(user => user.NormalizedPhone == identifier.NormalizedValue),
            AuthIdentifierChannel.Email => users.Where(user => user.NormalizedEmail == identifier.NormalizedValue),
            _ => users.Where(user => false),
        };
}
