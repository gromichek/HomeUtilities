using HomeUtilities.Core.Tariffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class VatRateConfiguration : IEntityTypeConfiguration<VatRate>, IEntityTypeConfiguration<VatRateVersion>
{
    public void Configure(EntityTypeBuilder<VatRate> builder)
    {
        builder.ToTable("VatRates");
        builder.Property(v => v.Code).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(v => v.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(v => v.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasIndex(v => v.Code).IsUnique();
        builder.HasMany(v => v.Versions).WithOne().HasForeignKey(v => v.VatRateId).OnDelete(DeleteBehavior.ClientCascade);
    }

    public void Configure(EntityTypeBuilder<VatRateVersion> builder) => builder.ToTable("VatRateVersions");
}
