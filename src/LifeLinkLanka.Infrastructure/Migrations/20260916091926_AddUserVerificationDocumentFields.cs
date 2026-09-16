using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeLinkLanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVerificationDocumentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VerificationDocumentName",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VerificationDocumentType",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VerificationDocumentUrl",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationDocumentName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationDocumentType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationDocumentUrl",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "AspNetUsers");
        }
    }
}
