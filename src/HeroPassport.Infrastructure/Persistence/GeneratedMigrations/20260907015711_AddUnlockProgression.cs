using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HeroPassport.Infrastructure.Persistence.GeneratedMigrations
{
    /// <inheritdoc />
    public partial class AddUnlockProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quest_milestones",
                columns: table => new
                {
                    quest_report_id = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    event_key = table.Column<string>(type: "TEXT", nullable: false),
                    semantic_key = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_milestones", x => new { x.quest_report_id, x.ordinal });
                    table.CheckConstraint("ck_quest_milestones_event_key", "length(event_key) BETWEEN 1 AND 80");
                    table.CheckConstraint("ck_quest_milestones_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_quest_milestones_semantic_key", "length(semantic_key) BETWEEN 1 AND 160");
                    table.ForeignKey(
                        name: "FK_quest_milestones_quest_reports_quest_report_id",
                        column: x => x.quest_report_id,
                        principalTable: "quest_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "titles",
                columns: table => new
                {
                    title_key = table.Column<string>(type: "TEXT", nullable: false),
                    catalog_version = table.Column<string>(type: "TEXT", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_titles", x => x.title_key);
                    table.CheckConstraint("ck_titles_catalog_version", "length(catalog_version) BETWEEN 1 AND 40");
                    table.CheckConstraint("ck_titles_key", "title_key IN ('rising_adventurer','veteran_of_the_merge','skill_specialist','unbroken_builder','master_of_many_tools')");
                    table.CheckConstraint("ck_titles_priority", "priority BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "traits",
                columns: table => new
                {
                    trait_key = table.Column<string>(type: "TEXT", nullable: false),
                    catalog_version = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traits", x => x.trait_key);
                    table.CheckConstraint("ck_traits_catalog_version", "length(catalog_version) BETWEEN 1 AND 40");
                    table.CheckConstraint("ck_traits_key", "trait_key IN ('precise_executor','test_scout','scope_keeper','steady_hand','polyglot_crafter')");
                });

            migrationBuilder.CreateTable(
                name: "hero_titles",
                columns: table => new
                {
                    hero_id = table.Column<string>(type: "TEXT", nullable: false),
                    title_key = table.Column<string>(type: "TEXT", nullable: false),
                    source_quest_id = table.Column<string>(type: "TEXT", nullable: true),
                    unlocked_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hero_titles", x => new { x.hero_id, x.title_key });
                    table.ForeignKey(
                        name: "FK_hero_titles_heroes_hero_id",
                        column: x => x.hero_id,
                        principalTable: "heroes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hero_titles_quest_sessions_source_quest_id",
                        column: x => x.source_quest_id,
                        principalTable: "quest_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hero_titles_titles_title_key",
                        column: x => x.title_key,
                        principalTable: "titles",
                        principalColumn: "title_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hero_traits",
                columns: table => new
                {
                    hero_id = table.Column<string>(type: "TEXT", nullable: false),
                    trait_key = table.Column<string>(type: "TEXT", nullable: false),
                    source_quest_id = table.Column<string>(type: "TEXT", nullable: true),
                    unlocked_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hero_traits", x => new { x.hero_id, x.trait_key });
                    table.ForeignKey(
                        name: "FK_hero_traits_heroes_hero_id",
                        column: x => x.hero_id,
                        principalTable: "heroes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hero_traits_quest_sessions_source_quest_id",
                        column: x => x.source_quest_id,
                        principalTable: "quest_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_hero_traits_traits_trait_key",
                        column: x => x.trait_key,
                        principalTable: "traits",
                        principalColumn: "trait_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "titles",
                columns: new[] { "title_key", "catalog_version", "priority" },
                values: new object[,]
                {
                    { "master_of_many_tools", "unlock/2.0.0", 5 },
                    { "rising_adventurer", "unlock/2.0.0", 1 },
                    { "skill_specialist", "unlock/2.0.0", 3 },
                    { "unbroken_builder", "unlock/2.0.0", 4 },
                    { "veteran_of_the_merge", "unlock/2.0.0", 2 }
                });

            migrationBuilder.InsertData(
                table: "traits",
                columns: new[] { "trait_key", "catalog_version" },
                values: new object[,]
                {
                    { "polyglot_crafter", "unlock/2.0.0" },
                    { "precise_executor", "unlock/2.0.0" },
                    { "scope_keeper", "unlock/2.0.0" },
                    { "steady_hand", "unlock/2.0.0" },
                    { "test_scout", "unlock/2.0.0" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_hero_titles_source_quest_id",
                table: "hero_titles",
                column: "source_quest_id");

            migrationBuilder.CreateIndex(
                name: "IX_hero_titles_title_key",
                table: "hero_titles",
                column: "title_key");

            migrationBuilder.CreateIndex(
                name: "IX_hero_traits_source_quest_id",
                table: "hero_traits",
                column: "source_quest_id");

            migrationBuilder.CreateIndex(
                name: "IX_hero_traits_trait_key",
                table: "hero_traits",
                column: "trait_key");

            migrationBuilder.CreateIndex(
                name: "ux_titles_priority",
                table: "titles",
                column: "priority",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hero_titles");

            migrationBuilder.DropTable(
                name: "hero_traits");

            migrationBuilder.DropTable(
                name: "quest_milestones");

            migrationBuilder.DropTable(
                name: "titles");

            migrationBuilder.DropTable(
                name: "traits");
        }
    }
}
