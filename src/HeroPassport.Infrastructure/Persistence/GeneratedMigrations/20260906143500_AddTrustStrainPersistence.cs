using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeroPassport.Infrastructure.Persistence.GeneratedMigrations
{
    [DbContext(typeof(HeroPassportDbContext))]
    [Migration("20260906143500_AddTrustStrainPersistence")]
    public partial class AddTrustStrainPersistence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quest_trust_strain_components",
                columns: table => new
                {
                    quest_report_id = table.Column<string>(type: "TEXT", nullable: false),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    component_key = table.Column<string>(type: "TEXT", nullable: false),
                    trust_delta = table.Column<int>(type: "INTEGER", nullable: false),
                    strain_delta = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_trust_strain_components", x => new { x.quest_report_id, x.ordinal });
                    table.CheckConstraint("ck_quest_trust_strain_components_key", "length(component_key) BETWEEN 1 AND 80");
                    table.CheckConstraint("ck_quest_trust_strain_components_nonzero", "trust_delta <> 0 OR strain_delta <> 0");
                    table.CheckConstraint("ck_quest_trust_strain_components_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_quest_trust_strain_components_strain_delta", "strain_delta BETWEEN -100 AND 100");
                    table.CheckConstraint("ck_quest_trust_strain_components_trust_delta", "trust_delta BETWEEN -100 AND 100");
                    table.ForeignKey(
                        name: "FK_quest_trust_strain_components_quest_reports_quest_report_id",
                        column: x => x.quest_report_id,
                        principalTable: "quest_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_quest_trust_strain_components_report_key",
                table: "quest_trust_strain_components",
                columns: new[] { "quest_report_id", "component_key" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "quest_trust_strain_components");
        }
    }
}
