using Microsoft.EntityFrameworkCore;

namespace HeroPassport.Infrastructure.Persistence;

internal static class TrustStrainStorageModel
{
    private const string QuestReport = "HeroPassport.Storage.QuestReport";
    private const string QuestTrustStrainComponent = "HeroPassport.Storage.QuestTrustStrainComponent";

    public static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity(QuestTrustStrainComponent, entity =>
        {
            entity.Property<string>("quest_report_id").HasColumnType("TEXT");
            entity.Property<int>("ordinal").HasColumnType("INTEGER");
            entity.Property<string>("component_key").HasColumnType("TEXT").IsRequired();
            entity.Property<int>("trust_delta").HasColumnType("INTEGER");
            entity.Property<int>("strain_delta").HasColumnType("INTEGER");

            entity.HasKey("quest_report_id", "ordinal");
            entity.HasOne(QuestReport).WithMany().HasForeignKey("quest_report_id").OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex("quest_report_id", "component_key")
                .IsUnique()
                .HasDatabaseName("ux_quest_trust_strain_components_report_key");

            entity.ToTable("quest_trust_strain_components", table =>
            {
                table.HasCheckConstraint("ck_quest_trust_strain_components_ordinal", "ordinal >= 0");
                table.HasCheckConstraint("ck_quest_trust_strain_components_key", "length(component_key) BETWEEN 1 AND 80");
                table.HasCheckConstraint("ck_quest_trust_strain_components_trust_delta", "trust_delta BETWEEN -100 AND 100");
                table.HasCheckConstraint("ck_quest_trust_strain_components_strain_delta", "strain_delta BETWEEN -100 AND 100");
                table.HasCheckConstraint("ck_quest_trust_strain_components_nonzero", "trust_delta <> 0 OR strain_delta <> 0");
            });
        });
    }
}
