using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoccerCheckin.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FamilyId",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Families",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HeadUserSessionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Families", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Families_UserSessions_HeadUserSessionId",
                        column: x => x.HeadUserSessionId,
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserSessionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyInvites_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    UserSessionId = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_UserSessions_UserSessionId",
                        column: x => x.UserSessionId,
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_FamilyId",
                table: "Players",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_Families_HeadUserSessionId",
                table: "Families",
                column: "HeadUserSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyInvites_FamilyId",
                table: "FamilyInvites",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyInvites_Token",
                table: "FamilyInvites",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_FamilyId",
                table: "FamilyMembers",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_UserSessionId",
                table: "FamilyMembers",
                column: "UserSessionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Families_FamilyId",
                table: "Players",
                column: "FamilyId",
                principalTable: "Families",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Families_FamilyId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "FamilyInvites");

            migrationBuilder.DropTable(
                name: "FamilyMembers");

            migrationBuilder.DropTable(
                name: "Families");

            migrationBuilder.DropIndex(
                name: "IX_Players_FamilyId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "Players");
        }
    }
}
