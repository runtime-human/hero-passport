using Microsoft.EntityFrameworkCore;

namespace HeroPassport.Infrastructure.Persistence;

internal static class RewardSkillStorageModel
{
    private const string Hero = "HeroPassport.Storage.Hero";
    private const string QuestReport = "HeroPassport.Storage.QuestReport";
    private const string Skill = "HeroPassport.Storage.Skill";
    private const string HeroSkill = "HeroPassport.Storage.HeroSkill";
    private const string QuestRewardComponent = "HeroPassport.Storage.QuestRewardComponent";
    private const string QuestReportSkill = "HeroPassport.Storage.QuestReportSkill";

    private static readonly string[] CanonicalSkills =
    [
        "coding",
        "testing_awareness",
        "scope_control",
        "documentation",
        "tool_use",
        "planning",
        "research",
        "debugging",
        "review",
        "maintenance",
    ];

    public static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureSkillCatalog(modelBuilder);
        ConfigureHeroSkills(modelBuilder);
        ConfigureRewardComponents(modelBuilder);
        ConfigureReportSkills(modelBuilder);
    }

    private static void ConfigureSkillCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(Skill, entity =>
        {
            entity.Property<string>("skill_key").HasColumnType("TEXT");
            entity.HasKey("skill_key");
            entity.ToTable("skills", table =>
                table.HasCheckConstraint(
                    "ck_skills_key",
                    "skill_key IN ('coding','testing_awareness','scope_control','documentation','tool_use','planning','research','debugging','review','maintenance')"));

            entity.HasData(CanonicalSkills.Select(static skill => new { skill_key = skill }).ToArray());
        });
    }

    private static void ConfigureHeroSkills(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(HeroSkill, entity =>
        {
            entity.Property<string>("hero_id").HasColumnType("TEXT");
            entity.Property<string>("skill_key").HasColumnType("TEXT");
            entity.Property<long>("xp").HasColumnType("INTEGER");
            entity.Property<string>("updated_at_utc").HasColumnType("TEXT").IsRequired();

            entity.HasKey("hero_id", "skill_key");
            entity.HasOne(Hero)
                .WithMany()
                .HasForeignKey("hero_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Skill)
                .WithMany()
                .HasForeignKey("skill_key")
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("hero_skills", table =>
                table.HasCheckConstraint("ck_hero_skills_xp", "xp BETWEEN 0 AND 9007199254740991"));
        });
    }

    private static void ConfigureRewardComponents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(QuestRewardComponent, entity =>
        {
            entity.Property<string>("quest_report_id").HasColumnType("TEXT");
            entity.Property<int>("ordinal").HasColumnType("INTEGER");
            entity.Property<string>("component_key").HasColumnType("TEXT").IsRequired();
            entity.Property<long>("xp_delta").HasColumnType("INTEGER");

            entity.HasKey("quest_report_id", "ordinal");
            entity.HasOne(QuestReport)
                .WithMany()
                .HasForeignKey("quest_report_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("quest_reward_components", table =>
            {
                table.HasCheckConstraint("ck_quest_reward_components_ordinal", "ordinal >= 0");
                table.HasCheckConstraint("ck_quest_reward_components_key", "length(component_key) BETWEEN 1 AND 80");
                table.HasCheckConstraint("ck_quest_reward_components_delta", "xp_delta BETWEEN -9007199254740991 AND 9007199254740991");
            });
        });
    }

    private static void ConfigureReportSkills(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(QuestReportSkill, entity =>
        {
            entity.Property<string>("quest_report_id").HasColumnType("TEXT");
            entity.Property<int>("ordinal").HasColumnType("INTEGER");
            entity.Property<string>("skill_key").HasColumnType("TEXT").IsRequired();
            entity.Property<long>("xp_gained").HasColumnType("INTEGER");
            entity.Property<long>("xp_before").HasColumnType("INTEGER");
            entity.Property<long>("xp_after").HasColumnType("INTEGER");
            entity.Property<int?>("level_before").HasColumnType("INTEGER");
            entity.Property<int?>("level_after").HasColumnType("INTEGER");

            entity.HasKey("quest_report_id", "ordinal");
            entity.HasIndex("quest_report_id", "skill_key")
                .IsUnique()
                .HasDatabaseName("ux_quest_report_skills_report_skill");
            entity.HasOne(QuestReport)
                .WithMany()
                .HasForeignKey("quest_report_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Skill)
                .WithMany()
                .HasForeignKey("skill_key")
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("quest_report_skills", table =>
            {
                table.HasCheckConstraint("ck_quest_report_skills_ordinal", "ordinal BETWEEN 0 AND 2");
                table.HasCheckConstraint("ck_quest_report_skills_xp_gained", "xp_gained BETWEEN 0 AND 9007199254740991");
                table.HasCheckConstraint("ck_quest_report_skills_xp_before", "xp_before BETWEEN 0 AND 9007199254740991");
                table.HasCheckConstraint("ck_quest_report_skills_xp_after", "xp_after BETWEEN 0 AND 9007199254740991");
                table.HasCheckConstraint("ck_quest_report_skills_xp_monotonic", "xp_after = xp_before + xp_gained");
                table.HasCheckConstraint("ck_quest_report_skills_level_before", "level_before IS NULL OR level_before BETWEEN 1 AND 10");
                table.HasCheckConstraint("ck_quest_report_skills_level_after", "level_after IS NULL OR level_after BETWEEN 1 AND 10");
            });
        });
    }
}
