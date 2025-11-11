using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    ISBN = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    BookTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BookAuthor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    YearOfPublication = table.Column<int>(type: "int", nullable: false),
                    Publisher = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImageUrlS = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrlM = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrlL = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.ISBN);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Books");
        }
    }
}
