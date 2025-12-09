using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerProfileAndBookListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DeliveryState column already exists from previous migration, skip it
            
            migrationBuilder.CreateTable(
                name: "SellerProfiles",
                columns: table => new
                {
                    SellerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", nullable: false, defaultValue: 0.0m),
                    TotalSales = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalBooksSold = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerProfiles", x => x.SellerId);
                    table.ForeignKey(
                        name: "FK_SellerProfiles_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SellerBookListings",
                columns: table => new
                {
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookISBN = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Condition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerBookListings", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_SellerBookListings_SellerProfiles_SellerId",
                        column: x => x.SellerId,
                        principalTable: "SellerProfiles",
                        principalColumn: "SellerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SellerBookListings_BookISBN",
                table: "SellerBookListings",
                column: "BookISBN");

            migrationBuilder.CreateIndex(
                name: "IX_SellerBookListings_IsActive",
                table: "SellerBookListings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SellerBookListings_SellerId",
                table: "SellerBookListings",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerBookListings_SellerId_BookISBN_Condition",
                table: "SellerBookListings",
                columns: new[] { "SellerId", "BookISBN", "Condition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_Location",
                table: "SellerProfiles",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_SellerProfiles_Rating",
                table: "SellerProfiles",
                column: "Rating");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SellerBookListings");

            migrationBuilder.DropTable(
                name: "SellerProfiles");

            // DeliveryState column should not be dropped as it was added in a previous migration
        }
    }
}
