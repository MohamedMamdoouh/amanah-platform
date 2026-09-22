using Amanah.Api.Tests.Infrastructure;

namespace Amanah.Api.Tests.OpenApi;

public class SwaggerDocumentTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Swagger_v1_document_includes_auth_login()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/auth/login", json, StringComparison.Ordinal);
    }
}
