using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SellerReviews",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerReviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_SellerReviews_SellerProfiles_SellerId",
                        column: x => x.SellerId,
                        principalTable: "SellerProfiles",
                        principalColumn: "SellerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SellerReviews_CustomerId",
                table: "SellerReviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerReviews_OrderId",
                table: "SellerReviews",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerReviews_OrderId_SellerId_CustomerId",
                table: "SellerReviews",
                columns: new[] { "OrderId", "SellerId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerReviews_Rating",
                table: "SellerReviews",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_SellerReviews_SellerId",
                table: "SellerReviews",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SellerReviews");
        }
    }
}
