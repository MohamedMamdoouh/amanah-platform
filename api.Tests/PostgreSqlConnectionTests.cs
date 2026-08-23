using Npgsql;

namespace Amanah.Api.Tests;

public class PostgreSqlConnectionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly string _connectionString = factory.ConnectionString;

    [Fact]
    public async Task Postgre_sql_container_accepts_connections()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (var versionCommand = new NpgsqlCommand("SHOW server_version", connection))
        {
            var version = (string)(await versionCommand.ExecuteScalarAsync())!;
            Assert.StartsWith("16.", version);
        }

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1, Convert.ToInt32(result));
    }
}
