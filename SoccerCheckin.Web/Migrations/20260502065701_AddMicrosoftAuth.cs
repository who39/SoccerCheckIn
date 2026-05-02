using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerCheckin.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddMicrosoftAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MicrosoftDisplayName",
                table: "UserSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MicrosoftEmail",
                table: "UserSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MicrosoftId",
                table: "UserSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MicrosoftDisplayName",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "MicrosoftEmail",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "MicrosoftId",
                table: "UserSessions");
        }
    }
}
