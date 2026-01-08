using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAllocationAndSettlementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    AllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmountCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PlatformFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlatformFeeCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SellerPayout = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellerPayoutCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidOutAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.AllocationId);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SellerSettlements",
                columns: table => new
                {
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalPayout = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayoutCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerSettlements", x => x.SettlementId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_OrderId",
                table: "PaymentAllocations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_SellerId",
                table: "PaymentAllocations",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_Status",
                table: "PaymentAllocations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlements_SellerId",
                table: "SellerSettlements",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlements_SellerId_PeriodStart_PeriodEnd",
                table: "SellerSettlements",
                columns: new[] { "SellerId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlements_Status",
                table: "SellerSettlements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "SellerSettlements");
        }
    }
}
