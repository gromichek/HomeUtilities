using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class ImportConfiguration :
    IEntityTypeConfiguration<ImportBatch>,
    IEntityTypeConfiguration<SavedImportProfile>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches");
        builder.Property(b => b.FileName).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(b => b.SheetName).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(b => b.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasReference<ImportBatch, SavedImportProfile>(b => b.ProfileId);
        builder.HasReference<ImportBatch, Contract>(b => b.ContractId);
    }

    public void Configure(EntityTypeBuilder<SavedImportProfile> builder)
    {
        builder.ToTable("ImportProfiles");
        builder.Property(p => p.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(p => p.Profile).HasJsonColumn();
    }
}

internal static class ImportBatchReferences
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bill>().HasReference<Bill, ImportBatch>(b => b.ImportBatchId);
        modelBuilder.Entity<Payment>().HasReference<Payment, ImportBatch>(p => p.ImportBatchId);
        modelBuilder.Entity<MeterReading>().HasReference<MeterReading, ImportBatch>(r => r.ImportBatchId);
    }
}
