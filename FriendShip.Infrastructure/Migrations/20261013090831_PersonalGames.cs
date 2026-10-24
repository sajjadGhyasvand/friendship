using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendShip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersonalGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentGameId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameLinkCode",
                table: "Users",
                type: "varchar(24)",
                unicode: false,
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryCodeHash",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Users",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Rooms",
                type: "varchar(24)",
                unicode: false,
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(6)",
                oldUnicode: false,
                oldMaxLength: 6);

            migrationBuilder.AddColumn<bool>(
                name: "IsPersonalGame",
                table: "Rooms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GeneratorVersion",
                table: "RoomQuestions",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerPrompt",
                table: "RoomQuestions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CurrentGameId",
                table: "Users",
                column: "CurrentGameId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GameLinkCode",
                table: "Users",
                column: "GameLinkCode",
                unique: true,
                filter: "[GameLinkCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true,
                filter: "[UserName] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Rooms_CurrentGameId",
                table: "Users",
                column: "CurrentGameId",
                principalTable: "Rooms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM Rooms WHERE LEN(Code) > 6) OR EXISTS (SELECT 1 FROM Users WHERE UserName IS NOT NULL) THROW 51000, 'Rollback would remove new account access or truncate game links. Restore a reviewed backup instead.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Rooms_CurrentGameId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CurrentGameId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GameLinkCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentGameId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GameLinkCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RecoveryCodeHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsPersonalGame",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "GeneratorVersion",
                table: "RoomQuestions");

            migrationBuilder.DropColumn(
                name: "OwnerPrompt",
                table: "RoomQuestions");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Rooms",
                type: "varchar(6)",
                unicode: false,
                maxLength: 6,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(24)",
                oldUnicode: false,
                oldMaxLength: 24);
        }
    }
}
