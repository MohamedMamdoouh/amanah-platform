using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAnonymizedUserAndAddRefreshTokenRevokedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE refresh_tokens
                SET "RevokedAt" = "ExpiresAt"
                WHERE "IsRevoked" = true AND "RevokedAt" IS NULL;
                """);

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-4000-8000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "refresh_tokens");
        }
    }
}
