using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualDurationAndProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageModel",
                table: "Projects",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ImageStyle",
                table: "Projects",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "ManualImageDurationSec",
                table: "Projects",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PipelineProgress",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UniqueImageCount",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageModel",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ImageStyle",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ManualImageDurationSec",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PipelineProgress",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UniqueImageCount",
                table: "Projects");
        }
    }
}
