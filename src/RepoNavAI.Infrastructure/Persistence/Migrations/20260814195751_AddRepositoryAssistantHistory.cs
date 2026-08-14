using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryAssistantHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryAssistantHistory",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DisplayTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    OrientationPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsStarred = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryAssistantHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryAssistantHistory_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "reponav",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryAssistantHistory_RegisteredRepositories_Repositor~",
                        column: x => x.RepositoryId,
                        principalSchema: "reponav",
                        principalTable: "RegisteredRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryAssistantHistory_RepositoryOrientationPlans_Orien~",
                        column: x => x.OrientationPlanId,
                        principalSchema: "reponav",
                        principalTable: "RepositoryOrientationPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RepositoryAssistantHistory_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "reponav",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAssistantHistory_OrganizationId_CreatedAtUtc",
                schema: "reponav",
                table: "RepositoryAssistantHistory",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAssistantHistory_OrganizationId_RepositoryId_User~",
                schema: "reponav",
                table: "RepositoryAssistantHistory",
                columns: new[] { "OrganizationId", "RepositoryId", "UserId", "IsStarred", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAssistantHistory_OrientationPlanId",
                schema: "reponav",
                table: "RepositoryAssistantHistory",
                column: "OrientationPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAssistantHistory_RepositoryId",
                schema: "reponav",
                table: "RepositoryAssistantHistory",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAssistantHistory_UserId",
                schema: "reponav",
                table: "RepositoryAssistantHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryAssistantHistory",
                schema: "reponav");
        }
    }
}
