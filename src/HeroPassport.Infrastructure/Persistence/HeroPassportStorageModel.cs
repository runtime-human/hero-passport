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
    private const string QuestReport = "HeroPassport.Storage.QuestReport";
    private const string XpEvent = "HeroPassport.Storage.XpEvent";

    public static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureHero(modelBuilder);
        ConfigureProject(modelBuilder);
        ConfigureSettings(modelBuilder);
        ConfigureQuest(modelBuilder);
        ConfigureMutationReceipt(modelBuilder);
        ConfigureHeroProjectStats(modelBuilder);
        ConfigureQuestReport(modelBuilder);
        RewardSkillStorageModel.Configure(modelBuilder);
        ConfigureXpEvent(modelBuilder);
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

    private static void ConfigureQuestReport(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(QuestReport, entity =>
        {
            entity.Property<string>("id").HasColumnType("TEXT");
            entity.Property<string>("quest_id").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("result").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("summary").HasColumnType("TEXT").IsRequired();
            entity.Property<int>("tests_mentioned").HasColumnType("INTEGER");
            entity.Property<int>("scope_violations").HasColumnType("INTEGER");
            entity.Property<int>("user_corrections").HasColumnType("INTEGER");
            entity.Property<string>("build_status").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("build_evidence").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("tests_status").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("tests_evidence").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("finalization_args_encoding_version").HasColumnType("TEXT").IsRequired();
            entity.Property<byte[]>("finalization_args_hash").HasColumnType("BLOB").IsRequired();
            entity.Property<string>("reward_rule_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("hero_progression_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("skill_progression_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("skill_allocation_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("trust_strain_rule_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("streak_rule_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("unlock_rule_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("rank_rule_version").HasColumnType("TEXT").IsRequired();
            entity.Property<int>("base_xp").HasColumnType("INTEGER");
            entity.Property<int>("bonus_xp").HasColumnType("INTEGER");
            entity.Property<int>("penalty_xp").HasColumnType("INTEGER");
            entity.Property<int>("raw_xp").HasColumnType("INTEGER");
            entity.Property<int>("outcome_permille").HasColumnType("INTEGER");
            entity.Property<long>("xp_gained").HasColumnType("INTEGER");
            entity.Property<long>("hero_total_xp_before").HasColumnType("INTEGER");
            entity.Property<long>("hero_total_xp_after").HasColumnType("INTEGER");
            entity.Property<int>("hero_level_before").HasColumnType("INTEGER");
            entity.Property<int>("hero_level_after").HasColumnType("INTEGER");
            entity.Property<string>("rank_before").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("rank_after").HasColumnType("TEXT").IsRequired();
            entity.Property<int>("trust_before").HasColumnType("INTEGER");
            entity.Property<int>("trust_after").HasColumnType("INTEGER");
            entity.Property<int>("strain_before").HasColumnType("INTEGER");
            entity.Property<int>("strain_after").HasColumnType("INTEGER");
            entity.Property<long>("streak_before").HasColumnType("INTEGER");
            entity.Property<long>("streak_after").HasColumnType("INTEGER");
            entity.Property<string?>("active_title_before").HasColumnType("TEXT");
            entity.Property<string?>("active_title_after").HasColumnType("TEXT");
            entity.Property<string>("created_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("id");
            entity.HasOne(Quest)
                .WithMany()
                .HasForeignKey("quest_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex("quest_id")
                .IsUnique()
                .HasDatabaseName("ux_quest_reports_quest_id");
            entity.ToTable("quest_reports", table =>
            {
                table.HasCheckConstraint("ck_quest_reports_result", "result IN ('success','partial','blocked','failed','abandoned')");
                table.HasCheckConstraint("ck_quest_reports_summary", "length(summary) BETWEEN 1 AND 2000");
                table.HasCheckConstraint("ck_quest_reports_tests_mentioned", "tests_mentioned IN (0,1)");
                table.HasCheckConstraint("ck_quest_reports_scope_violations", "scope_violations BETWEEN 0 AND 20");
                table.HasCheckConstraint("ck_quest_reports_user_corrections", "user_corrections BETWEEN 0 AND 20");
                table.HasCheckConstraint("ck_quest_reports_build_status", "build_status IN ('not_run','passed','failed','unknown')");
                table.HasCheckConstraint("ck_quest_reports_build_evidence", "build_evidence IN ('observed','reported','none')");
                table.HasCheckConstraint("ck_quest_reports_tests_status", "tests_status IN ('not_run','passed','failed','unknown')");
                table.HasCheckConstraint("ck_quest_reports_tests_evidence", "tests_evidence IN ('observed','reported','none')");
                table.HasCheckConstraint("ck_quest_reports_finalization_hash", "length(finalization_args_hash) = 32");
                table.HasCheckConstraint("ck_quest_reports_xp_components", "base_xp >= 0 AND bonus_xp >= 0 AND penalty_xp >= 0 AND raw_xp >= 0 AND xp_gained >= 0");
                table.HasCheckConstraint("ck_quest_reports_outcome_permille", "outcome_permille IN (0,100,300,600,1000)");
                table.HasCheckConstraint("ck_quest_reports_total_xp", "hero_total_xp_before BETWEEN 0 AND 9007199254740991 AND hero_total_xp_after BETWEEN 0 AND 9007199254740991");
                table.HasCheckConstraint("ck_quest_reports_levels", "hero_level_before BETWEEN 1 AND 50 AND hero_level_after BETWEEN 1 AND 50");
                table.HasCheckConstraint("ck_quest_reports_trust", "trust_before BETWEEN 0 AND 100 AND trust_after BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_quest_reports_strain", "strain_before BETWEEN 0 AND 100 AND strain_after BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_quest_reports_streak", "streak_before >= 0 AND streak_after >= 0");
            });
        });
    }

    private static void ConfigureXpEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(XpEvent, entity =>
        {
            entity.Property<string>("id").HasColumnType("TEXT");
            entity.Property<string>("quest_id").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("hero_id").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("project_id").HasColumnType("TEXT").IsRequired();
            entity.Property<long>("xp_delta").HasColumnType("INTEGER");
            entity.Property<string>("reward_rule_version").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("created_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("id");
            entity.HasOne(Quest)
                .WithMany()
                .HasForeignKey("quest_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Hero)
                .WithMany()
                .HasForeignKey("hero_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Project)
                .WithMany()
                .HasForeignKey("project_id")
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex("quest_id")
                .IsUnique()
                .HasDatabaseName("ux_xp_events_quest_id");
            entity.ToTable("xp_events", table =>
            {
                table.HasCheckConstraint("ck_xp_events_xp_delta", "xp_delta >= 0");
            });
        });
    }
}
