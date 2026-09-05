using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeroPassport.Infrastructure.Persistence.GeneratedMigrations
{
    /// <inheritdoc />
    public partial class AddMutationReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mutation_receipts",
                columns: table => new
                {
                    operation_key = table.Column<string>(type: "TEXT", nullable: false),
                    request_id = table.Column<string>(type: "TEXT", nullable: false),
                    args_encoding_version = table.Column<string>(type: "TEXT", nullable: false),
                    args_hash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    effective_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    hero_id = table.Column<string>(type: "TEXT", nullable: true),
                    project_id = table.Column<string>(type: "TEXT", nullable: true),
                    result_entity_id = table.Column<string>(type: "TEXT", nullable: true),
                    result_kind = table.Column<string>(type: "TEXT", nullable: false),
                    result_status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mutation_receipts", x => new { x.operation_key, x.request_id });
                    table.CheckConstraint("ck_mutation_receipts_args_hash", "length(args_hash) = 32");
                    table.CheckConstraint("ck_mutation_receipts_args_version", "length(args_encoding_version) BETWEEN 1 AND 64");
                    table.CheckConstraint("ck_mutation_receipts_operation", "operation_key IN ('bootstrap','create_hero','start_quest','finish_quest')");
                    table.CheckConstraint("ck_mutation_receipts_result_kind", "result_kind IN ('bootstrap','hero','quest_start','quest_finish')");
                    table.CheckConstraint("ck_mutation_receipts_result_status", "result_status IN ('active','target_deleted')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mutation_receipts");
        }
    }
}
