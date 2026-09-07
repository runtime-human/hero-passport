using Microsoft.EntityFrameworkCore;

namespace HeroPassport.Infrastructure.Persistence;

internal static class UnlockStorageModel
{
    private const string Hero = "HeroPassport.Storage.Hero";
    private const string Quest = "HeroPassport.Storage.QuestSession";
    private const string QuestReport = "HeroPassport.Storage.QuestReport";
    private const string Trait = "HeroPassport.Storage.Trait";
    private const string HeroTrait = "HeroPassport.Storage.HeroTrait";
    private const string Title = "HeroPassport.Storage.Title";
    private const string HeroTitle = "HeroPassport.Storage.HeroTitle";
    private const string QuestMilestone = "HeroPassport.Storage.QuestMilestone";

    private const string CatalogVersion = "unlock/2.0.0";

    public static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ConfigureTrait(modelBuilder);
        ConfigureTitle(modelBuilder);
        ConfigureHeroTrait(modelBuilder);
        ConfigureHeroTitle(modelBuilder);
        ConfigureQuestMilestone(modelBuilder);
    }

    private static void ConfigureTrait(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(Trait, entity =>
        {
            entity.Property<string>("trait_key").HasColumnType("TEXT");
            entity.Property<string>("catalog_version").HasColumnType("TEXT").IsRequired();
            entity.HasKey("trait_key");
            entity.ToTable("traits", table =>
            {
                table.HasCheckConstraint(
                    "ck_traits_key",
                    "trait_key IN ('precise_executor','test_scout','scope_keeper','steady_hand','polyglot_crafter')");
                table.HasCheckConstraint("ck_traits_catalog_version", "length(catalog_version) BETWEEN 1 AND 40");
            });
            entity.HasData(
                new { trait_key = "precise_executor", catalog_version = CatalogVersion },
                new { trait_key = "test_scout", catalog_version = CatalogVersion },
                new { trait_key = "scope_keeper", catalog_version = CatalogVersion },
                new { trait_key = "steady_hand", catalog_version = CatalogVersion },
                new { trait_key = "polyglot_crafter", catalog_version = CatalogVersion });
        });
    }

    private static void ConfigureTitle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(Title, entity =>
        {
            entity.Property<string>("title_key").HasColumnType("TEXT");
            entity.Property<int>("priority").HasColumnType("INTEGER");
            entity.Property<string>("catalog_version").HasColumnType("TEXT").IsRequired();
            entity.HasKey("title_key");
            entity.HasIndex("priority").IsUnique().HasDatabaseName("ux_titles_priority");
            entity.ToTable("titles", table =>
            {
                table.HasCheckConstraint(
                    "ck_titles_key",
                    "title_key IN ('rising_adventurer','veteran_of_the_merge','skill_specialist','unbroken_builder','master_of_many_tools')");
                table.HasCheckConstraint("ck_titles_priority", "priority BETWEEN 1 AND 5");
                table.HasCheckConstraint("ck_titles_catalog_version", "length(catalog_version) BETWEEN 1 AND 40");
            });
            entity.HasData(
                new { title_key = "rising_adventurer", priority = 1, catalog_version = CatalogVersion },
                new { title_key = "veteran_of_the_merge", priority = 2, catalog_version = CatalogVersion },
                new { title_key = "skill_specialist", priority = 3, catalog_version = CatalogVersion },
                new { title_key = "unbroken_builder", priority = 4, catalog_version = CatalogVersion },
                new { title_key = "master_of_many_tools", priority = 5, catalog_version = CatalogVersion });
        });
    }

    private static void ConfigureHeroTrait(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(HeroTrait, entity =>
        {
            entity.Property<string>("hero_id").HasColumnType("TEXT");
            entity.Property<string>("trait_key").HasColumnType("TEXT");
            entity.Property<string>("unlocked_at_utc").HasColumnType("TEXT").IsRequired();
            entity.Property<string?>("source_quest_id").HasColumnType("TEXT");
            entity.HasKey("hero_id", "trait_key");
            entity.HasOne(Hero).WithMany().HasForeignKey("hero_id").OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Trait).WithMany().HasForeignKey("trait_key").OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(Quest).WithMany().HasForeignKey("source_quest_id").OnDelete(DeleteBehavior.SetNull);
            entity.ToTable("hero_traits");
        });
    }

    private static void ConfigureHeroTitle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(HeroTitle, entity =>
        {
            entity.Property<string>("hero_id").HasColumnType("TEXT");
            entity.Property<string>("title_key").HasColumnType("TEXT");
            entity.Property<string>("unlocked_at_utc").HasColumnType("TEXT").IsRequired();
            entity.Property<string?>("source_quest_id").HasColumnType("TEXT");
            entity.HasKey("hero_id", "title_key");
            entity.HasOne(Hero).WithMany().HasForeignKey("hero_id").OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(Title).WithMany().HasForeignKey("title_key").OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(Quest).WithMany().HasForeignKey("source_quest_id").OnDelete(DeleteBehavior.SetNull);
            entity.ToTable("hero_titles");
        });
    }

    private static void ConfigureQuestMilestone(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(QuestMilestone, entity =>
        {
            entity.Property<string>("quest_report_id").HasColumnType("TEXT");
            entity.Property<int>("ordinal").HasColumnType("INTEGER");
            entity.Property<string>("event_key").HasColumnType("TEXT").IsRequired();
            entity.Property<string>("semantic_key").HasColumnType("TEXT").IsRequired();
            entity.HasKey("quest_report_id", "ordinal");
            entity.HasOne(QuestReport).WithMany().HasForeignKey("quest_report_id").OnDelete(DeleteBehavior.Cascade);
            entity.ToTable("quest_milestones", table =>
            {
                table.HasCheckConstraint("ck_quest_milestones_ordinal", "ordinal >= 0");
                table.HasCheckConstraint("ck_quest_milestones_event_key", "length(event_key) BETWEEN 1 AND 80");
                table.HasCheckConstraint("ck_quest_milestones_semantic_key", "length(semantic_key) BETWEEN 1 AND 160");
            });
        });
    }
}
