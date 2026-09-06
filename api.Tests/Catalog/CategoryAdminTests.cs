using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Data;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Responses.Auth;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Catalog;

public class CategoryAdminTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Create_category_visible_in_admin_list_and_public_catalog_when_active()
    {
        await using var scope = await CreateSeededScopeAsync();
        var client = factory.CreateClient();
        await LoginAsAdminAsync(client);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new CreateCategoryRequest
            {
                Code = "jewelry",
                SortOrder = 9,
                PhotosPrivate = false,
                IsActive = true,
            });

        var created = await createResponse.Content.ReadFromJsonAsync<AdminCategoryResponse>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("jewelry", created.Code);
        Assert.Equal(9, created.SortOrder);
        Assert.True(created.IsActive);

        var adminList = await client.GetFromJsonAsync<AdminCategoryListResponse>("/api/v1/admin/categories");
        Assert.NotNull(adminList);
        Assert.Contains(adminList.Items, category => category.Code == "jewelry");

        client.DefaultRequestHeaders.Authorization = null;
        var publicList = await client.GetFromJsonAsync<CategoryListResponse>("/api/v1/categories");
        Assert.NotNull(publicList);
        Assert.Contains(publicList.Items, category => category.Code == "jewelry");
    }

    [Fact]
    public async Task Deactivate_category_hides_from_public_catalog_but_keeps_in_admin_list()
    {
        await using var scope = await CreateSeededScopeAsync();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = factory.CreateClient();
        await LoginAsAdminAsync(client);

        var category = await context.Categories.SingleAsync(category => category.Code == "other");
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/admin/categories/{category.Id}",
            new UpdateCategoryRequest
            {
                Code = category.Code,
                SortOrder = category.SortOrder,
                PhotosPrivate = category.PhotosPrivate,
                IsActive = false,
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var publicList = await client.GetFromJsonAsync<CategoryListResponse>("/api/v1/categories");
        Assert.NotNull(publicList);
        Assert.DoesNotContain(publicList.Items, item => item.Code == "other");

        await LoginAsAdminAsync(client);
        var adminList = await client.GetFromJsonAsync<AdminCategoryListResponse>("/api/v1/admin/categories");
        Assert.NotNull(adminList);
        Assert.Contains(adminList.Items, item => item.Code == "other" && !item.IsActive);
    }

    [Fact]
    public async Task Create_and_update_field_definition_persists_validation_bounds()
    {
        await using var scope = await CreateSeededScopeAsync();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = factory.CreateClient();
        await LoginAsAdminAsync(client);

        var phones = await context.Categories
            .Include(category => category.FieldDefinitions)
            .SingleAsync(category => category.Code == "phones");

        var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/categories/{phones.Id}/fields",
            new CreateCategoryFieldRequest
            {
                FieldKey = "imei_last_digits",
                Type = "text",
                Required = false,
                SortOrder = 3,
                MinLength = 4,
                MaxLength = 4,
            });

        var created = await createResponse.Content.ReadFromJsonAsync<AdminCategoryFieldDefinitionResponse>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("imei_last_digits", created.FieldKey);
        Assert.Equal(4, created.MinLength);
        Assert.Equal(4, created.MaxLength);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/admin/categories/{phones.Id}/fields/{created.Id}",
            new UpdateCategoryFieldRequest
            {
                FieldKey = "imei_last_digits",
                Type = "text",
                Required = true,
                SortOrder = 3,
                MinLength = 3,
                MaxLength = 6,
            });

        var updated = await updateResponse.Content.ReadFromJsonAsync<AdminCategoryFieldDefinitionResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.True(updated.Required);
        Assert.Equal(3, updated.MinLength);
        Assert.Equal(6, updated.MaxLength);

        client.DefaultRequestHeaders.Authorization = null;
        var publicList = await client.GetFromJsonAsync<CategoryListResponse>("/api/v1/categories");
        var publicPhones = publicList!.Items.Single(category => category.Code == "phones");
        var publicField = publicPhones.FieldDefinitions.Single(field => field.FieldKey == "imei_last_digits");
        Assert.True(publicField.Required);
        Assert.Equal(3, publicField.MinLength);
        Assert.Equal(6, publicField.MaxLength);
    }

    [Fact]
    public async Task Create_category_with_duplicate_code_returns_conflict()
    {
        await using var scope = await CreateSeededScopeAsync();
        var client = factory.CreateClient();
        await LoginAsAdminAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new CreateCategoryRequest
            {
                Code = "phones",
                SortOrder = 99,
                PhotosPrivate = false,
                IsActive = true,
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task LoginAsAdminAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { phone = "01011111111", password = "AdminPass123" });
        var adminSession = await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);
    }

    private async Task<AsyncServiceScope> CreateSeededScopeAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
        return scope;
    }
}
