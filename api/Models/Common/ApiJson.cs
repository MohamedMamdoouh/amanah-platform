using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amanah.Api.Models.Common;

public static class ApiJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
