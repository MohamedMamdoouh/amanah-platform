using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpSmsOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "otp_sms_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OtpCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProtectedPayload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_sms_outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_otp_sms_outbox_otp_codes_OtpCodeId",
                        column: x => x.OtpCodeId,
                        principalTable: "otp_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_otp_sms_outbox_OtpCodeId",
                table: "otp_sms_outbox",
                column: "OtpCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_otp_sms_outbox_Phone_Status_ProcessedAt",
                table: "otp_sms_outbox",
                columns: new[] { "Phone", "Status", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_sms_outbox_Status_CreatedAt",
                table: "otp_sms_outbox",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "otp_sms_outbox");
        }
    }
}
