using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeroPassport.Infrastructure.Persistence.GeneratedMigrations
{
    /// <inheritdoc />
    public partial class AddHeroProjectStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hero_project_stats",
                columns: table => new
                {
                    hero_id = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<string>(type: "TEXT", nullable: false),
                    last_quest_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    quests_finished = table.Column<long>(type: "INTEGER", nullable: false),
                    quests_started = table.Column<long>(type: "INTEGER", nullable: false),
                    quests_succeeded = table.Column<long>(type: "INTEGER", nullable: false),
                    total_xp_earned = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hero_project_stats", x => new { x.hero_id, x.project_id });
                    table.CheckConstraint("ck_hero_project_stats_quests_finished", "quests_finished >= 0");
                    table.CheckConstraint("ck_hero_project_stats_quests_started", "quests_started >= 0");
                    table.CheckConstraint("ck_hero_project_stats_quests_succeeded", "quests_succeeded >= 0");
                    table.CheckConstraint("ck_hero_project_stats_total_xp_earned", "total_xp_earned >= 0");
                    table.ForeignKey(
                        name: "FK_hero_project_stats_heroes_hero_id",
                        column: x => x.hero_id,
                        principalTable: "heroes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hero_project_stats_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hero_project_stats_project_id",
                table: "hero_project_stats",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hero_project_stats");
        }
    }
}
