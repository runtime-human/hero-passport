using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeroPassport.Infrastructure.Persistence.GeneratedMigrations
{
    /// <inheritdoc />
    public partial class AddRewardSkillPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quest_reward_components",
                columns: table => new
                {
                    quest_report_id = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    component_key = table.Column<string>(type: "TEXT", nullable: false),
                    xp_delta = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_reward_components", x => new { x.quest_report_id, x.ordinal });
                    table.CheckConstraint("ck_quest_reward_components_delta", "xp_delta BETWEEN -9007199254740991 AND 9007199254740991 AND xp_delta <> 0");
                    table.CheckConstraint("ck_quest_reward_components_key", "length(component_key) BETWEEN 1 AND 80");
                    table.CheckConstraint("ck_quest_reward_components_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "FK_quest_reward_components_quest_reports_quest_report_id",
                        column: x => x.quest_report_id,
                        principalTable: "quest_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    skill_key = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.skill_key);
                    table.CheckConstraint("ck_skills_key", "skill_key IN ('coding','testing_awareness','scope_control','documentation','tool_use','planning','research','debugging','review','maintenance')");
                });

            migrationBuilder.CreateTable(
                name: "hero_skills",
                columns: table => new
                {
                    hero_id = table.Column<string>(type: "TEXT", nullable: false),
                    skill_key = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    xp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hero_skills", x => new { x.hero_id, x.skill_key });
                    table.CheckConstraint("ck_hero_skills_xp", "xp BETWEEN 0 AND 9007199254740991");
                    table.ForeignKey(
                        name: "FK_hero_skills_heroes_hero_id",
                        column: x => x.hero_id,
                        principalTable: "heroes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hero_skills_skills_skill_key",
                        column: x => x.skill_key,
                        principalTable: "skills",
                        principalColumn: "skill_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quest_report_skills",
                columns: table => new
                {
                    quest_report_id = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    level_after = table.Column<int>(type: "INTEGER", nullable: false),
                    level_before = table.Column<int>(type: "INTEGER", nullable: false),
                    skill_key = table.Column<string>(type: "TEXT", nullable: false),
                    xp_after = table.Column<long>(type: "INTEGER", nullable: false),
                    xp_before = table.Column<long>(type: "INTEGER", nullable: false),
                    xp_gained = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_report_skills", x => new { x.quest_report_id, x.ordinal });
                    table.CheckConstraint("ck_quest_report_skills_level_after", "level_after BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_quest_report_skills_level_before", "level_before BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_quest_report_skills_ordinal", "ordinal BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_quest_report_skills_xp_after", "xp_after BETWEEN 0 AND 9007199254740991");
                    table.CheckConstraint("ck_quest_report_skills_xp_before", "xp_before BETWEEN 0 AND 9007199254740991");
                    table.CheckConstraint("ck_quest_report_skills_xp_gained", "xp_gained BETWEEN 0 AND 9007199254740991");
                    table.CheckConstraint("ck_quest_report_skills_xp_monotonic", "xp_after = xp_before + xp_gained");
                    table.ForeignKey(
                        name: "FK_quest_report_skills_quest_reports_quest_report_id",
                        column: x => x.quest_report_id,
                        principalTable: "quest_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quest_report_skills_skills_skill_key",
                        column: x => x.skill_key,
                        principalTable: "skills",
                        principalColumn: "skill_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "skills",
                column: "skill_key",
                values: new object[]
                {
                    "coding",
                    "debugging",
                    "documentation",
                    "maintenance",
                    "planning",
                    "research",
                    "review",
                    "scope_control",
                    "testing_awareness",
                    "tool_use"
                });

            migrationBuilder.CreateIndex(
                name: "IX_hero_skills_skill_key",
                table: "hero_skills",
                column: "skill_key");

            migrationBuilder.CreateIndex(
                name: "IX_quest_report_skills_skill_key",
                table: "quest_report_skills",
                column: "skill_key");

            migrationBuilder.CreateIndex(
                name: "ux_quest_report_skills_report_skill",
                table: "quest_report_skills",
                columns: new[] { "quest_report_id", "skill_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_quest_reward_components_report_key",
                table: "quest_reward_components",
                columns: new[] { "quest_report_id", "component_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hero_skills");

            migrationBuilder.DropTable(
                name: "quest_report_skills");

            migrationBuilder.DropTable(
                name: "quest_reward_components");

            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
