using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractAutoShorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExtractAutoShorts",
                table: "Projects",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractAutoShorts",
                table: "Projects");
        }
    }
}
