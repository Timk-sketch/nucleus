using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContentHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Feature = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_usages_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "banned_words",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banned_words", x => x.Id);
                    table.ForeignKey(
                        name: "FK_banned_words_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "blog_post"),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "draft"),
                    HtmlContent = table.Column<string>(type: "text", nullable: true),
                    SeoTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MetaDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AiModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AiPrompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WordCount = table.Column<int>(type: "integer", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_pages_brand_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "brand_keywords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_content_pages_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "blog_post"),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsGlobal = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_templates_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usages_BrandId",
                table: "ai_usages",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usages_TenantId",
                table: "ai_usages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usages_TenantId_BrandId",
                table: "ai_usages",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usages_TenantId_Feature_CreatedAt",
                table: "ai_usages",
                columns: new[] { "TenantId", "Feature", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_banned_words_BrandId_Word",
                table: "banned_words",
                columns: new[] { "BrandId", "Word" });

            migrationBuilder.CreateIndex(
                name: "IX_banned_words_TenantId",
                table: "banned_words",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_banned_words_TenantId_BrandId",
                table: "banned_words",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_content_pages_BrandId_KeywordId",
                table: "content_pages",
                columns: new[] { "BrandId", "KeywordId" });

            migrationBuilder.CreateIndex(
                name: "IX_content_pages_BrandId_ScheduledAt",
                table: "content_pages",
                columns: new[] { "BrandId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_content_pages_BrandId_Status",
                table: "content_pages",
                columns: new[] { "BrandId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_content_pages_KeywordId",
                table: "content_pages",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_content_pages_TenantId",
                table: "content_pages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_content_pages_TenantId_BrandId",
                table: "content_pages",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_content_templates_BrandId_IsActive",
                table: "content_templates",
                columns: new[] { "BrandId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_content_templates_BrandId_PageType",
                table: "content_templates",
                columns: new[] { "BrandId", "PageType" });

            migrationBuilder.CreateIndex(
                name: "IX_content_templates_TenantId",
                table: "content_templates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_content_templates_TenantId_BrandId",
                table: "content_templates",
                columns: new[] { "TenantId", "BrandId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usages");

            migrationBuilder.DropTable(
                name: "banned_words");

            migrationBuilder.DropTable(
                name: "content_pages");

            migrationBuilder.DropTable(
                name: "content_templates");
        }
    }
}
