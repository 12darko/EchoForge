using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToYouTubeChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "YouTubeChannels",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "YouTubeChannels");
        }
    }
}
