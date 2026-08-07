using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryEndpointCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryEndpoints",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Route = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Handler = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Line = table.Column<int>(type: "integer", nullable: false),
                    RequiresAuthorization = table.Column<bool>(type: "boolean", nullable: false),
                    DownstreamSymbols = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryEndpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryEndpoints_RepositorySnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "reponav",
                        principalTable: "RepositorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryEndpoints_SnapshotId_HttpMethod_Route_Handler",
                schema: "reponav",
                table: "RepositoryEndpoints",
                columns: new[] { "SnapshotId", "HttpMethod", "Route", "Handler" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryEndpoints",
                schema: "reponav");
        }
    }
}
