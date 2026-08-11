using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryFavorites",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryFavorites_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "reponav",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryFavorites_RegisteredRepositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "reponav",
                        principalTable: "RegisteredRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "reponav",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFavorites_OrganizationId_UserId",
                schema: "reponav",
                table: "RepositoryFavorites",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFavorites_OrganizationId_UserId_RepositoryId",
                schema: "reponav",
                table: "RepositoryFavorites",
                columns: new[] { "OrganizationId", "UserId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFavorites_RepositoryId",
                schema: "reponav",
                table: "RepositoryFavorites",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFavorites_UserId",
                schema: "reponav",
                table: "RepositoryFavorites",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryFavorites",
                schema: "reponav");
        }
    }
}
