using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalAuthenticationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalAuthenticationCodes",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAuthenticationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalAuthenticationCodes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "reponav",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuthenticationCodes_CodeHash",
                schema: "reponav",
                table: "ExternalAuthenticationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuthenticationCodes_ExpiresAtUtc",
                schema: "reponav",
                table: "ExternalAuthenticationCodes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuthenticationCodes_UserId",
                schema: "reponav",
                table: "ExternalAuthenticationCodes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalAuthenticationCodes",
                schema: "reponav");
        }
    }
}
