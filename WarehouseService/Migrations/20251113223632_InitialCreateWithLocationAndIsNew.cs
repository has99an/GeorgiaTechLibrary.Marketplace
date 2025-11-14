using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarehouseService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithLocationAndIsNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                table: "WarehouseItems");

            migrationBuilder.AddColumn<bool>(
                name: "IsNew",
                table: "WarehouseItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "WarehouseItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Main Warehouse");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNew",
                table: "WarehouseItems");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "WarehouseItems");

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "WarehouseItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
