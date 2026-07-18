using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuthorityHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backlink_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AnchorText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DomainRating = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backlink_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_backlink_records_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "brand_mentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MentionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Sentiment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "neutral"),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_mentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_brand_mentions_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outreach_queue_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OutreachAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outreach_queue_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outreach_queue_items_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schema_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SchemaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schema_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_schema_templates_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backlink_records_BrandId_FirstSeenAt",
                table: "backlink_records",
                columns: new[] { "BrandId", "FirstSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_backlink_records_BrandId_IsActive",
                table: "backlink_records",
                columns: new[] { "BrandId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_backlink_records_TenantId",
                table: "backlink_records",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_backlink_records_TenantId_BrandId",
                table: "backlink_records",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_brand_mentions_BrandId_DiscoveredAt",
                table: "brand_mentions",
                columns: new[] { "BrandId", "DiscoveredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_brand_mentions_BrandId_IsReviewed",
                table: "brand_mentions",
                columns: new[] { "BrandId", "IsReviewed" });

            migrationBuilder.CreateIndex(
                name: "IX_brand_mentions_TenantId",
                table: "brand_mentions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_brand_mentions_TenantId_BrandId",
                table: "brand_mentions",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_outreach_queue_items_BrandId_Status",
                table: "outreach_queue_items",
                columns: new[] { "BrandId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_outreach_queue_items_TenantId",
                table: "outreach_queue_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_outreach_queue_items_TenantId_BrandId",
                table: "outreach_queue_items",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_schema_templates_BrandId_IsActive",
                table: "schema_templates",
                columns: new[] { "BrandId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_schema_templates_BrandId_PageType",
                table: "schema_templates",
                columns: new[] { "BrandId", "PageType" });

            migrationBuilder.CreateIndex(
                name: "IX_schema_templates_TenantId",
                table: "schema_templates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_schema_templates_TenantId_BrandId",
                table: "schema_templates",
                columns: new[] { "TenantId", "BrandId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backlink_records");

            migrationBuilder.DropTable(
                name: "brand_mentions");

            migrationBuilder.DropTable(
                name: "outreach_queue_items");

            migrationBuilder.DropTable(
                name: "schema_templates");
        }
    }
}
