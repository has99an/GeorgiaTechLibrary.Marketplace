using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCountry",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPostalCode",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryState",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStreet",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Add temporary column
            migrationBuilder.AddColumn<int>(
                name: "StatusTemp",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Migrate data: Convert string status to enum int
            // "Pending" -> 0, "Shipped" -> 2 (Fulfilled), others -> 0 (Pending)
            migrationBuilder.Sql(@"
                UPDATE OrderItems 
                SET StatusTemp = CASE 
                    WHEN Status = 'Pending' THEN 0
                    WHEN Status = 'Shipped' THEN 2
                    ELSE 0
                END
            ");

            // Drop old column
            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderItems");

            // Rename temp column to Status
            migrationBuilder.RenameColumn(
                name: "StatusTemp",
                table: "OrderItems",
                newName: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryCountry",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPostalCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryState",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryStreet",
                table: "Orders");

            // Add temporary string column
            migrationBuilder.AddColumn<string>(
                name: "StatusTemp",
                table: "OrderItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            // Migrate data: Convert enum int to string
            migrationBuilder.Sql(@"
                UPDATE OrderItems 
                SET StatusTemp = CASE 
                    WHEN Status = 0 THEN 'Pending'
                    WHEN Status = 1 THEN 'Pending'
                    WHEN Status = 2 THEN 'Shipped'
                    WHEN Status = 3 THEN 'Pending'
                    WHEN Status = 4 THEN 'Pending'
                    ELSE 'Pending'
                END
            ");

            // Drop int column
            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderItems");

            // Rename temp column to Status
            migrationBuilder.RenameColumn(
                name: "StatusTemp",
                table: "OrderItems",
                newName: "Status");
        }
    }
}
