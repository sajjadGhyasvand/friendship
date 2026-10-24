using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendShip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiscoveryAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScoringVersion",
                table: "Rooms",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "RoomQuestions",
                type: "varchar(48)",
                unicode: false,
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateVersion",
                table: "RoomQuestions",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscoveryProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowComparison = table.Column<bool>(type: "bit", nullable: false),
                    AllowRelationships = table.Column<bool>(type: "bit", nullable: false),
                    AllowMbti = table.Column<bool>(type: "bit", nullable: false),
                    ComparisonCode = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_DiscoveryProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisDecisions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultKey = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    State = table.Column<int>(type: "int", nullable: true),
                    Correction = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    ShareCode = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisDecisions", x => new { x.UserId, x.ResultKey });
                    table.ForeignKey(
                        name: "FK_AnalysisDecisions_DiscoveryProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "DiscoveryProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveryAnswers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionKey = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: false),
                    QuestionVersion = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OptionIndex = table.Column<int>(type: "int", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsSkipped = table.Column<bool>(type: "bit", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryAnswers", x => new { x.UserId, x.QuestionKey });
                    table.ForeignKey(
                        name: "FK_DiscoveryAnswers_DiscoveryProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "DiscoveryProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisDecisions_ShareCode",
                table: "AnalysisDecisions",
                column: "ShareCode",
                unique: true,
                filter: "[ShareCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryProfiles_ComparisonCode",
                table: "DiscoveryProfiles",
                column: "ComparisonCode",
                unique: true,
                filter: "[ComparisonCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisDecisions");

            migrationBuilder.DropTable(
                name: "DiscoveryAnswers");

            migrationBuilder.DropTable(
                name: "DiscoveryProfiles");

            migrationBuilder.DropColumn(
                name: "ScoringVersion",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "RoomQuestions");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "RoomQuestions");
        }
    }
}
