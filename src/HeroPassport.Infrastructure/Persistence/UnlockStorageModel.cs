using HeroPassport.Domain.Engine;
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
                table.HasCheckConstraint("ck_traits_key", InConstraint("trait_key", UnlockRules.TraitKeys));
                table.HasCheckConstraint("ck_traits_catalog_version", "length(catalog_version) BETWEEN 1 AND 40");
            });
            foreach (var traitKey in UnlockRules.TraitKeys)
            {
                entity.HasData(new { trait_key = traitKey, catalog_version = UnlockRules.RuleVersion });
            }
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
                    InConstraint("title_key", UnlockRules.TitleCatalog.Select(static title => title.TitleKey)));
                table.HasCheckConstraint(
                    "ck_titles_priority",
                    $"priority BETWEEN {UnlockRules.TitleCatalog.Min(static title => title.Priority)} AND {UnlockRules.TitleCatalog.Max(static title => title.Priority)}");
                table.HasCheckConstraint("ck_titles_catalog_version", "length(catalog_version) BETWEEN 1 AND 40");
            });
            foreach (var title in UnlockRules.TitleCatalog)
            {
                entity.HasData(new
                {
                    title_key = title.TitleKey,
                    priority = title.Priority,
                    catalog_version = UnlockRules.RuleVersion,
                });
            }
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

    private static string InConstraint(string columnName, IEnumerable<string> values) =>
        $"{columnName} IN ({string.Join(',', values.Select(static value => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'"))})";
}
