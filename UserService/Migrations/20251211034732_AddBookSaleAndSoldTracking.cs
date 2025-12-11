using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddBookSaleAndSoldTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSold",
                table: "SellerBookListings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoldDate",
                table: "SellerBookListings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookSales",
                columns: table => new
                {
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BookISBN = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    SellerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookSales", x => x.SaleId);
                    table.ForeignKey(
                        name: "FK_BookSales_SellerBookListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "SellerBookListings",
                        principalColumn: "ListingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookSales_SellerProfiles_SellerId",
                        column: x => x.SellerId,
                        principalTable: "SellerProfiles",
                        principalColumn: "SellerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SellerBookListings_IsSold",
                table: "SellerBookListings",
                column: "IsSold");

            migrationBuilder.CreateIndex(
                name: "IX_BookSales_BookISBN",
                table: "BookSales",
                column: "BookISBN");

            migrationBuilder.CreateIndex(
                name: "IX_BookSales_BuyerId",
                table: "BookSales",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_BookSales_ListingId",
                table: "BookSales",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookSales_OrderId",
                table: "BookSales",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_BookSales_SaleDate",
                table: "BookSales",
                column: "SaleDate");

            migrationBuilder.CreateIndex(
                name: "IX_BookSales_SellerId",
                table: "BookSales",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookSales");

            migrationBuilder.DropIndex(
                name: "IX_SellerBookListings_IsSold",
                table: "SellerBookListings");

            migrationBuilder.DropColumn(
                name: "IsSold",
                table: "SellerBookListings");

            migrationBuilder.DropColumn(
                name: "SoldDate",
                table: "SellerBookListings");
        }
    }
}
