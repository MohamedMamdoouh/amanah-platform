using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueOtpCodeDestinationChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM otp_codes older
                USING otp_codes newer
                WHERE older."Destination" = newer."Destination"
                  AND older."Channel" = newer."Channel"
                  AND older."CreatedAt" < newer."CreatedAt";
                """);

            migrationBuilder.DropIndex(
                name: "IX_otp_codes_Destination_Channel",
                table: "otp_codes");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_Destination_Channel",
                table: "otp_codes",
                columns: new[] { "Destination", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_otp_codes_Destination_Channel",
                table: "otp_codes");

            migrationBuilder.CreateIndex(
                name: "IX_otp_codes_Destination_Channel",
                table: "otp_codes",
                columns: new[] { "Destination", "Channel" });
        }
    }
}
