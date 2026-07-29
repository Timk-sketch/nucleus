using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinderV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomCss",
                table: "finders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "finders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColorOverride",
                table: "finders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WhiteLabelEnabled",
                table: "finders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LeadEmail",
                table: "finder_sessions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadName",
                table: "finder_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeadPhone",
                table: "finder_sessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                table: "finder_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "finder_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IntroTextOverride = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finder_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finder_variants_finders_FinderId",
                        column: x => x.FinderId,
                        principalTable: "finders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finder_sessions_VariantId",
                table: "finder_sessions",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_variants_FinderId",
                table: "finder_variants",
                column: "FinderId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_variants_TenantId",
                table: "finder_variants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_variants_TenantId_FinderId",
                table: "finder_variants",
                columns: new[] { "TenantId", "FinderId" });

            migrationBuilder.AddForeignKey(
                name: "FK_finder_sessions_finder_variants_VariantId",
                table: "finder_sessions",
                column: "VariantId",
                principalTable: "finder_variants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_finder_sessions_finder_variants_VariantId",
                table: "finder_sessions");

            migrationBuilder.DropTable(
                name: "finder_variants");

            migrationBuilder.DropIndex(
                name: "IX_finder_sessions_VariantId",
                table: "finder_sessions");

            migrationBuilder.DropColumn(
                name: "CustomCss",
                table: "finders");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "finders");

            migrationBuilder.DropColumn(
                name: "PrimaryColorOverride",
                table: "finders");

            migrationBuilder.DropColumn(
                name: "WhiteLabelEnabled",
                table: "finders");

            migrationBuilder.DropColumn(
                name: "LeadEmail",
                table: "finder_sessions");

            migrationBuilder.DropColumn(
                name: "LeadName",
                table: "finder_sessions");

            migrationBuilder.DropColumn(
                name: "LeadPhone",
                table: "finder_sessions");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "finder_sessions");
        }
    }
}
