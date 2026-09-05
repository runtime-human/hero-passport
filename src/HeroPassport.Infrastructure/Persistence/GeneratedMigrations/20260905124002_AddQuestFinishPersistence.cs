using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeroPassport.Infrastructure.Persistence.GeneratedMigrations
{
    /// <inheritdoc />
    public partial class AddQuestFinishPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quest_reports",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    active_title_after = table.Column<string>(type: "TEXT", nullable: true),
                    active_title_before = table.Column<string>(type: "TEXT", nullable: true),
                    base_xp = table.Column<int>(type: "INTEGER", nullable: false),
                    bonus_xp = table.Column<int>(type: "INTEGER", nullable: false),
                    build_evidence = table.Column<string>(type: "TEXT", nullable: false),
                    build_status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    finalization_args_encoding_version = table.Column<string>(type: "TEXT", nullable: false),
                    finalization_args_hash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    hero_level_after = table.Column<int>(type: "INTEGER", nullable: false),
                    hero_level_before = table.Column<int>(type: "INTEGER", nullable: false),
                    hero_progression_version = table.Column<string>(type: "TEXT", nullable: false),
                    hero_total_xp_after = table.Column<long>(type: "INTEGER", nullable: false),
                    hero_total_xp_before = table.Column<long>(type: "INTEGER", nullable: false),
                    outcome_permille = table.Column<int>(type: "INTEGER", nullable: false),
                    penalty_xp = table.Column<int>(type: "INTEGER", nullable: false),
                    quest_id = table.Column<string>(type: "TEXT", nullable: false),
                    rank_after = table.Column<string>(type: "TEXT", nullable: false),
                    rank_before = table.Column<string>(type: "TEXT", nullable: false),
                    rank_rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    raw_xp = table.Column<int>(type: "INTEGER", nullable: false),
                    result = table.Column<string>(type: "TEXT", nullable: false),
                    reward_rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    scope_violations = table.Column<int>(type: "INTEGER", nullable: false),
                    skill_allocation_version = table.Column<string>(type: "TEXT", nullable: false),
                    skill_progression_version = table.Column<string>(type: "TEXT", nullable: false),
                    strain_after = table.Column<int>(type: "INTEGER", nullable: false),
                    strain_before = table.Column<int>(type: "INTEGER", nullable: false),
                    streak_after = table.Column<long>(type: "INTEGER", nullable: false),
                    streak_before = table.Column<long>(type: "INTEGER", nullable: false),
                    streak_rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false),
                    tests_evidence = table.Column<string>(type: "TEXT", nullable: false),
                    tests_mentioned = table.Column<int>(type: "INTEGER", nullable: false),
                    tests_status = table.Column<string>(type: "TEXT", nullable: false),
                    trust_after = table.Column<int>(type: "INTEGER", nullable: false),
                    trust_before = table.Column<int>(type: "INTEGER", nullable: false),
                    trust_strain_rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    unlock_rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    user_corrections = table.Column<int>(type: "INTEGER", nullable: false),
                    xp_gained = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_reports", x => x.id);
                    table.CheckConstraint("ck_quest_reports_build_evidence", "build_evidence IN ('observed','reported','none')");
                    table.CheckConstraint("ck_quest_reports_build_status", "build_status IN ('not_run','passed','failed','unknown')");
                    table.CheckConstraint("ck_quest_reports_finalization_hash", "length(finalization_args_hash) = 32");
                    table.CheckConstraint("ck_quest_reports_levels", "hero_level_before BETWEEN 1 AND 50 AND hero_level_after BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_quest_reports_outcome_permille", "outcome_permille IN (0,100,300,600,1000)");
                    table.CheckConstraint("ck_quest_reports_result", "result IN ('success','partial','blocked','failed','abandoned')");
                    table.CheckConstraint("ck_quest_reports_scope_violations", "scope_violations BETWEEN 0 AND 20");
                    table.CheckConstraint("ck_quest_reports_strain", "strain_before BETWEEN 0 AND 100 AND strain_after BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_quest_reports_streak", "streak_before >= 0 AND streak_after >= 0");
                    table.CheckConstraint("ck_quest_reports_summary", "length(summary) BETWEEN 1 AND 2000");
                    table.CheckConstraint("ck_quest_reports_tests_evidence", "tests_evidence IN ('observed','reported','none')");
                    table.CheckConstraint("ck_quest_reports_tests_mentioned", "tests_mentioned IN (0,1)");
                    table.CheckConstraint("ck_quest_reports_tests_status", "tests_status IN ('not_run','passed','failed','unknown')");
                    table.CheckConstraint("ck_quest_reports_total_xp", "hero_total_xp_before BETWEEN 0 AND 9007199254740991 AND hero_total_xp_after BETWEEN 0 AND 9007199254740991");
                    table.CheckConstraint("ck_quest_reports_trust", "trust_before BETWEEN 0 AND 100 AND trust_after BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_quest_reports_user_corrections", "user_corrections BETWEEN 0 AND 20");
                    table.CheckConstraint("ck_quest_reports_xp_components", "base_xp >= 0 AND bonus_xp >= 0 AND penalty_xp >= 0 AND raw_xp >= 0 AND xp_gained >= 0");
                    table.ForeignKey(
                        name: "FK_quest_reports_quest_sessions_quest_id",
                        column: x => x.quest_id,
                        principalTable: "quest_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "xp_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    hero_id = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<string>(type: "TEXT", nullable: false),
                    quest_id = table.Column<string>(type: "TEXT", nullable: false),
                    reward_rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    xp_delta = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_events", x => x.id);
                    table.CheckConstraint("ck_xp_events_xp_delta", "xp_delta >= 0");
                    table.ForeignKey(
                        name: "FK_xp_events_heroes_hero_id",
                        column: x => x.hero_id,
                        principalTable: "heroes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_xp_events_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_xp_events_quest_sessions_quest_id",
                        column: x => x.quest_id,
                        principalTable: "quest_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_quest_reports_quest_id",
                table: "quest_reports",
                column: "quest_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_xp_events_hero_id",
                table: "xp_events",
                column: "hero_id");

            migrationBuilder.CreateIndex(
                name: "IX_xp_events_project_id",
                table: "xp_events",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_xp_events_quest_id",
                table: "xp_events",
                column: "quest_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quest_reports");

            migrationBuilder.DropTable(
                name: "xp_events");
        }
    }
}
