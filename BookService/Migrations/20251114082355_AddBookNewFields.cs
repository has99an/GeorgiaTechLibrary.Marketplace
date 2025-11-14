using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookService.Migrations
{
    /// <inheritdoc />
    public partial class AddBookNewFields : Migration
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
                    ImageUrlL = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Genre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    AvailabilityStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
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
