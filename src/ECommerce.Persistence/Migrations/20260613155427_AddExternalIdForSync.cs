using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdForSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Sizes",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Products",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Colors",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Categories",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Brands",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_ExternalId",
                table: "Sizes",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ExternalId",
                table: "Products",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Colors_ExternalId",
                table: "Colors",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ExternalId",
                table: "Categories",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_ExternalId",
                table: "Brands",
                column: "ExternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sizes_ExternalId",
                table: "Sizes");

            migrationBuilder.DropIndex(
                name: "IX_Products_ExternalId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Colors_ExternalId",
                table: "Colors");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ExternalId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_ExternalId",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Sizes");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Colors");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Brands");
        }
    }
}
