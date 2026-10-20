using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class TariffConfiguration : IEntityTypeConfiguration<TariffComponent>, IEntityTypeConfiguration<TariffRate>
{
    public void Configure(EntityTypeBuilder<TariffComponent> builder)
    {
        builder.ToTable("TariffComponents");
        builder.Property(t => t.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(t => t.QuantityParameterName).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(t => t.VatCode).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(t => t.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Property(t => t.RegisterKinds).HasJsonColumn();
        builder.Property(t => t.MeterIds).HasJsonColumn();
        builder.HasReference<TariffComponent, Contract>(t => t.ContractId);
        builder.HasReference<TariffComponent, Category>(t => t.CategoryId);
        builder.HasMany(t => t.Rates).WithOne().HasForeignKey(r => r.ComponentId).OnDelete(DeleteBehavior.ClientCascade);
    }

    public void Configure(EntityTypeBuilder<TariffRate> builder)
    {
        builder.ToTable("TariffRates");
        builder.Property(r => r.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Property(r => r.Tiers).HasJsonColumn();
        builder.HasIndex(r => new { r.ComponentId, r.ValidFrom });
    }
}
