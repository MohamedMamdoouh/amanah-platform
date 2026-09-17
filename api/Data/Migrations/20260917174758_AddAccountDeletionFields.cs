using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionRequestedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SenderAnonymizedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.InsertData(
                table: "users",
                columns: ["Id", "NormalizedPhone", "PasswordHash", "DisplayName", "Role", "IsBanned", "BanReason", "CreatedAt"],
                values: new object[]
                {
                    new Guid("00000000-0000-4000-8000-000000000001"),
                    "+000000000001",
                    "!",
                    "Deleted user",
                    "User",
                    true,
                    "System account for anonymized chat senders.",
                    new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero),
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-4000-8000-000000000001"));

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SenderAnonymizedAt",
                table: "users");
        }
    }
}
