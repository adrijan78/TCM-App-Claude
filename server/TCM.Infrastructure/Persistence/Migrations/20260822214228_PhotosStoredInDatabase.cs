using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhotosStoredInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Url",
                table: "Photos");

            migrationBuilder.AlterColumn<Guid>(
                name: "PublicId",
                table: "Photos",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                table: "Photos",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Photos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Photos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Photos",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SizeBytes",
                table: "Photos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Photos_PublicId",
                table: "Photos",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Photos_PublicId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Photos");

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Photos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Photos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }
    }
}
