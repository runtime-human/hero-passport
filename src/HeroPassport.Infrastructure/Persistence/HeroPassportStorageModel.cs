using Microsoft.EntityFrameworkCore;

namespace HeroPassport.Infrastructure.Persistence;

internal static class HeroPassportStorageModel
{
    private const string Hero = "HeroPassport.Storage.Hero";
    private const string Project = "HeroPassport.Storage.Project";
    private const string Settings = "HeroPassport.Storage.AppSettings";
    private const string Quest = "HeroPassport.Storage.QuestSession";
    private const string MutationReceipt = "HeroPassport.Storage.MutationReceipt";
    private const string HeroProjectStats = "HeroPassport.Storage.HeroProjectStats";

    public static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureHero(modelBuilder);
        ConfigureProject(modelBuilder);
        ConfigureSettings(modelBuilder);
        ConfigureQuest(modelBuilder);
        ConfigureMutationReceipt(modelBuilder);
        ConfigureHeroProjectStats(modelBuilder);
    }

    private static void ConfigureHero(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(Hero, entity =>
        {
            entity.Property<string>("id").HasColumnType("TEXT");
            entity.Property<string>("name").HasColumnType("TEXT").IsRequired();
            entity.Property<long>("total_xp").HasColumnType("INTEGER").HasDefaultValue(0L);
            entity.Property<int>("trust").HasColumnType("INTEGER").HasDefaultValue(50);
            entity.Property<int>("strain").HasColumnType("INTEGER").HasDefaultValue(20);
            entity.Property<long>("success_streak").HasColumnType("INTEGER").HasDefaultValue(0L);
            entity.Property<string?>("archived_at_utc").HasColumnType("TEXT");
            entity.Property<string>("created_at_utc").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("updated_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("id");
            entity.ToTable("heroes", table =>
            {
                table.HasCheckConstraint("ck_heroes_name", "length(name) BETWEEN 1 AND 64");
                table.HasCheckConstraint("ck_heroes_total_xp", "total_xp BETWEEN 0 AND 9007199254740991");
                table.HasCheckConstraint("ck_heroes_trust", "trust BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_heroes_strain", "strain BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_heroes_success_streak", "success_streak >= 0");
            });
        });
    }

    private static void ConfigureProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(Project, entity =>
        {
            entity.Property<string>("id").HasColumnType("TEXT");
            entity.Property<string>("display_name").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("workspace_fingerprint").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("identity_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("created_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("id");
            entity.HasIndex("workspace_fingerprint")
                .IsUnique()
                .HasDatabaseName("ux_projects_workspace_fingerprint");
            entity.ToTable("projects", table =>
            {
                table.HasCheckConstraint("ck_projects_display_name", "length(display_name) BETWEEN 1 AND 120");
                table.HasCheckConstraint("ck_projects_workspace_fingerprint", "length(workspace_fingerprint) = 64");
                table.HasCheckConstraint("ck_projects_identity_version", "identity_version = 'project-identity/1'");
            });
        });
    }

    private static void ConfigureSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(Settings, entity =>
        {
            entity.Property<int>("id").HasColumnType("INTEGER").ValueGeneratedNever();
            entity.Property<int>("setup_completed").HasColumnType("INTEGER");
            entity.Property<string?>("active_hero_id").HasColumnType("TEXT");
            entity.Property<string>("locale").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("presentation_style").HasColumnType("TEXT").IsRequired();
            entity.Property<int>("auto_start_quest").HasColumnType("INTEGER");
            entity.Property<int>("auto_finish_quest").HasColumnType("INTEGER");
            entity.Property<byte[]>("project_identity_salt_v1").HasColumnType("BLOB").IsRequired();
            entity.Property<int>("config_version").HasColumnType("INTEGER");
            entity.Property<string>("created_at_utc").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("updated_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("id");
            entity.HasOne(Hero)
                .WithMany()
                .HasForeignKey("active_hero_id")
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("app_settings", table =>
            {
                table.HasCheckConstraint("ck_app_settings_singleton", "id = 1");
                table.HasCheckConstraint("ck_app_settings_setup_completed", "setup_completed IN (0,1)");
                table.HasCheckConstraint("ck_app_settings_locale", "locale IN ('ru-RU','en-US')");
                table.HasCheckConstraint("ck_app_settings_presentation_style", "presentation_style IN ('rpg_engineering','classic_rpg','minimal')");
                table.HasCheckConstraint("ck_app_settings_auto_start", "auto_start_quest IN (0,1)");
                table.HasCheckConstraint("ck_app_settings_auto_finish", "auto_finish_quest IN (0,1)");
                table.HasCheckConstraint("ck_app_settings_salt", "length(project_identity_salt_v1) = 32");
                table.HasCheckConstraint("ck_app_settings_config_version", "config_version >= 1");
                table.HasCheckConstraint(
                    "ck_app_settings_setup_active_hero",
                    "(setup_completed = 0 AND active_hero_id IS NULL) OR (setup_completed = 1 AND active_hero_id IS NOT NULL)");
            });
        });
    }

    private static void ConfigureQuest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(Quest, entity =>
        {
            entity.Property<string>("id").HasColumnType("TEXT");
            entity.Property<string>("hero_id").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("project_id").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("quest_type").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("title").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("goal").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("locale").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("status").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("started_at_utc").HasColumnType("TEXT").IsRequired();
            entity.Property<string?>("finished_at_utc").HasColumnType("TEXT");
            entity.Property<string>("created_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("id");
            entity.HasOne(Hero)
                .WithMany()
                .HasForeignKey("hero_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Project)
                .WithMany()
                .HasForeignKey("project_id")
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex("hero_id", "project_id")
                .IsUnique()
                .HasDatabaseName("ux_quest_sessions_one_open_per_hero_project")
                .HasFilter("status = 'open'");
            entity.ToTable("quest_sessions", table =>
            {
                table.HasCheckConstraint("ck_quest_sessions_type", "quest_type IN ('planning','research','coding','review','debugging','documentation','maintenance')");
                table.HasCheckConstraint("ck_quest_sessions_title", "length(title) BETWEEN 1 AND 120");
                table.HasCheckConstraint("ck_quest_sessions_goal", "length(goal) BETWEEN 1 AND 500");
                table.HasCheckConstraint("ck_quest_sessions_locale", "locale IN ('ru-RU','en-US')");
                table.HasCheckConstraint("ck_quest_sessions_status", "status IN ('open','finished')");
                table.HasCheckConstraint(
                    "ck_quest_sessions_status_finished_at",
                    "(status = 'open' AND finished_at_utc IS NULL) OR (status = 'finished' AND finished_at_utc IS NOT NULL)");
            });
        });
    }

    private static void ConfigureMutationReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(MutationReceipt, entity =>
        {
            entity.Property<string>("operation_key").HasColumnType("TEXT");
            entity.Property<string>("request_id").HasColumnType("TEXT");
            entity.Property<string>("args_encoding_version").HasColumnType("TEXT").IsRequired();
            entity.Property<byte[]>("args_hash").HasColumnType("BLOB").IsRequired();
            entity.Property<string>("result_kind").HasColumnType("TEXT").IsRequired();
            entity.Property<string?>("result_entity_id").HasColumnType("TEXT");
            entity.Property<string?>("project_id").HasColumnType("TEXT");
            entity.Property<string?>("hero_id").HasColumnType("TEXT");
            entity.Property<string>("result_status").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("effective_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("operation_key", "request_id");
            entity.ToTable("mutation_receipts", table =>
            {
                table.HasCheckConstraint("ck_mutation_receipts_operation", "operation_key IN ('bootstrap','create_hero','start_quest','finish_quest')");
                table.HasCheckConstraint("ck_mutation_receipts_args_hash", "length(args_hash) = 32");
                table.HasCheckConstraint("ck_mutation_receipts_result_kind", "result_kind IN ('bootstrap','hero','quest_start','quest_finish')");
                table.HasCheckConstraint("ck_mutation_receipts_result_status", "result_status IN ('active','target_deleted')");
            });
        });
    }

    private static void ConfigureHeroProjectStats(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(HeroProjectStats, entity =>
        {
            entity.Property<string>("hero_id").HasColumnType("TEXT");
            entity.Property<string>("project_id").HasColumnType("TEXT");
            entity.Property<long>("quests_started").HasColumnType("INTEGER");
            entity.Property<long>("quests_finished").HasColumnType("INTEGER");
            entity.Property<long>("quests_succeeded").HasColumnType("INTEGER");
            entity.Property<long>("total_xp_earned").HasColumnType("INTEGER");
            entity.Property<string?>("last_quest_at_utc").HasColumnType("TEXT");

            entity.HasKey("hero_id", "project_id");
            entity.HasOne(Hero)
                .WithMany()
                .HasForeignKey("hero_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Project)
                .WithMany()
                .HasForeignKey("project_id")
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("hero_project_stats", table =>
            {
                table.HasCheckConstraint("ck_hero_project_stats_quests_started", "quests_started >= 0");
                table.HasCheckConstraint("ck_hero_project_stats_quests_finished", "quests_finished >= 0");
                table.HasCheckConstraint("ck_hero_project_stats_quests_succeeded", "quests_succeeded >= 0");
                table.HasCheckConstraint("ck_hero_project_stats_total_xp_earned", "total_xp_earned >= 0");
            });
        });
    }
}
