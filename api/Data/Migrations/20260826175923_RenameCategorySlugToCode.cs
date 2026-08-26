using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amanah.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameCategorySlugToCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "categories",
                newName: "Code");

            migrationBuilder.RenameIndex(
                name: "IX_categories_Slug",
                table: "categories",
                newName: "IX_categories_Code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Code",
                table: "categories",
                newName: "Slug");

            migrationBuilder.RenameIndex(
                name: "IX_categories_Code",
                table: "categories",
                newName: "IX_categories_Slug");
        }
    }
}
