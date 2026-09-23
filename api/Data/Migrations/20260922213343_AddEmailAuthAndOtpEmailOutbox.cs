using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAuthAndOtpEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NormalizedPhone",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "otp_codes",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Phone");

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "otp_codes",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE otp_codes
                SET "Destination" = "Phone", "Channel" = 'Phone'
                WHERE "Destination" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "otp_codes",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_otp_codes_Phone",
                table: "otp_codes");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "otp_codes");

            migrationBuilder.CreateTable(
                name: "otp_email_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OtpCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    ProtectedPayload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_email_outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_otp_email_outbox_otp_codes_OtpCodeId",
                        column: x => x.OtpCodeId,
                        principalTable: "otp_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_NormalizedEmail",
                table: "users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_at_least_one_identifier",
                table: "users",
                sql: "\"NormalizedPhone\" IS NOT NULL OR \"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_Destination_Channel",
                table: "otp_codes",
                columns: new[] { "Destination", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_email_outbox_Email_Status_ProcessedAt",
                table: "otp_email_outbox",
                columns: new[] { "Email", "Status", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_email_outbox_OtpCodeId",
                table: "otp_email_outbox",
                column: "OtpCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_otp_email_outbox_Status_CreatedAt",
                table: "otp_email_outbox",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "otp_email_outbox");

            migrationBuilder.DropIndex(
                name: "IX_users_NormalizedEmail",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_users_at_least_one_identifier",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_otp_codes_Destination_Channel",
                table: "otp_codes");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "otp_codes");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "otp_codes");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedPhone",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "otp_codes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_Phone",
                table: "otp_codes",
                column: "Phone");
        }
    }
}
