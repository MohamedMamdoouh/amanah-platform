using Amanah.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace Amanah.Api.Services.Auth;

public sealed class UserPasswordHasher
{
  private readonly PasswordHasher<User> _hasher = new();

  public string HashPassword(User user, string password) =>
      _hasher.HashPassword(user, password);

  public bool VerifyPassword(User user, string password, string passwordHash) =>
      _hasher.VerifyHashedPassword(user, passwordHash, password)
          is not PasswordVerificationResult.Failed;
}
