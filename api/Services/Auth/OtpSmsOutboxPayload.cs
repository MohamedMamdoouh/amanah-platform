using System.Text.Json;
using Amanah.Api.Models.Common;
using Microsoft.AspNetCore.DataProtection;

namespace Amanah.Api.Services.Auth;

public static class OtpSmsOutboxPayload
{
    private const string ProtectorPurpose = "OtpSmsOutbox";

    public static string Protect(IDataProtectionProvider dataProtectionProvider, string code)
    {
        var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var json = JsonSerializer.Serialize(code, ApiJson.SerializerOptions);
        return protector.Protect(json);
    }

    public static string Unprotect(IDataProtectionProvider dataProtectionProvider, string protectedPayload)
    {
        var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var json = protector.Unprotect(protectedPayload);
        var payload = JsonSerializer.Deserialize<string>(json, ApiJson.SerializerOptions)
            ?? throw new InvalidOperationException("OTP outbox payload is invalid.");

        return payload;
    }
}
