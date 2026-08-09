using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryOrientationPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryOrientationPlans",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Experience = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Focus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TimeBudgetMinutes = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    CompletedStepKeysJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryOrientationPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryOrientationPlans_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "reponav",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryOrientationPlans_RegisteredRepositories_Repositor~",
                        column: x => x.RepositoryId,
                        principalSchema: "reponav",
                        principalTable: "RegisteredRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryOrientationPlans_RepositorySnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "reponav",
                        principalTable: "RepositorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryOrientationPlans_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "reponav",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryOrientationPlans_OrganizationId_RepositoryId_User~",
                schema: "reponav",
                table: "RepositoryOrientationPlans",
                columns: new[] { "OrganizationId", "RepositoryId", "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryOrientationPlans_RepositoryId",
                schema: "reponav",
                table: "RepositoryOrientationPlans",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryOrientationPlans_SnapshotId",
                schema: "reponav",
                table: "RepositoryOrientationPlans",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryOrientationPlans_UserId",
                schema: "reponav",
                table: "RepositoryOrientationPlans",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryOrientationPlans",
                schema: "reponav");
        }
    }
}
