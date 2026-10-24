using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendShip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersonalizedFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAggregateSharing",
                table: "DiscoveryProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CopyVersion",
                table: "AnalysisDecisions",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceCount",
                table: "AnalysisDecisions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackAt",
                table: "AnalysisDecisions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleKey",
                table: "AnalysisDecisions",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowAggregateSharing",
                table: "DiscoveryProfiles");

            migrationBuilder.DropColumn(
                name: "CopyVersion",
                table: "AnalysisDecisions");

            migrationBuilder.DropColumn(
                name: "EvidenceCount",
                table: "AnalysisDecisions");

            migrationBuilder.DropColumn(
                name: "FeedbackAt",
                table: "AnalysisDecisions");

            migrationBuilder.DropColumn(
                name: "RuleKey",
                table: "AnalysisDecisions");
        }
    }
}
