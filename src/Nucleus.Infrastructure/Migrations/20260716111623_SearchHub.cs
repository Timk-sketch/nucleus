using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SearchHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "keyword_rank_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SearchVolume = table.Column<int>(type: "integer", nullable: true),
                    Competition = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_rank_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_keyword_rank_snapshots_brand_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "brand_keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_keyword_rank_snapshots_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "search_alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_search_alerts_brand_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "brand_keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_search_alerts_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topic_clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PillarKeyword = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClusterKeywordsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "planning"),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_clusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_topic_clusters_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_keyword_rank_snapshots_BrandId",
                table: "keyword_rank_snapshots",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_rank_snapshots_KeywordId_CheckedAt",
                table: "keyword_rank_snapshots",
                columns: new[] { "KeywordId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_keyword_rank_snapshots_TenantId",
                table: "keyword_rank_snapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_rank_snapshots_TenantId_BrandId",
                table: "keyword_rank_snapshots",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_search_alerts_BrandId",
                table: "search_alerts",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_search_alerts_KeywordId_IsActive",
                table: "search_alerts",
                columns: new[] { "KeywordId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_search_alerts_TenantId",
                table: "search_alerts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_search_alerts_TenantId_BrandId",
                table: "search_alerts",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_topic_clusters_BrandId",
                table: "topic_clusters",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_topic_clusters_TenantId",
                table: "topic_clusters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_topic_clusters_TenantId_BrandId",
                table: "topic_clusters",
                columns: new[] { "TenantId", "BrandId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "keyword_rank_snapshots");

            migrationBuilder.DropTable(
                name: "search_alerts");

            migrationBuilder.DropTable(
                name: "topic_clusters");
        }
    }
}
