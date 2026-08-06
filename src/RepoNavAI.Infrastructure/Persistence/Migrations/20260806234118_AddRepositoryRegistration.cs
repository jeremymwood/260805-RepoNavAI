using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegisteredRepositories",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderRepositoryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultBranch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WebUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredRepositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegisteredRepositories_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "reponav",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegisteredRepositories_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "reponav",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryIndexingRequests",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryIndexingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryIndexingRequests_RegisteredRepositories_Repositor~",
                        column: x => x.RepositoryId,
                        principalSchema: "reponav",
                        principalTable: "RegisteredRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryIndexingRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "reponav",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredRepositories_OrganizationId_Provider_Owner_Name",
                schema: "reponav",
                table: "RegisteredRepositories",
                columns: new[] { "OrganizationId", "Provider", "Owner", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredRepositories_RegisteredByUserId",
                schema: "reponav",
                table: "RegisteredRepositories",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryIndexingRequests_OrganizationId_Status",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryIndexingRequests_RepositoryId",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryIndexingRequests_RequestedByUserId",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryIndexingRequests",
                schema: "reponav");

            migrationBuilder.DropTable(
                name: "RegisteredRepositories",
                schema: "reponav");
        }
    }
}
