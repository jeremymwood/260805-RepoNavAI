using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryLanguageCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverageJson",
                schema: "reponav",
                table: "RepositorySnapshots",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "CoverageStatus",
                schema: "reponav",
                table: "RepositorySnapshots",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "none");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverageJson",
                schema: "reponav",
                table: "RepositorySnapshots");

            migrationBuilder.DropColumn(
                name: "CoverageStatus",
                schema: "reponav",
                table: "RepositorySnapshots");
        }
    }
}
