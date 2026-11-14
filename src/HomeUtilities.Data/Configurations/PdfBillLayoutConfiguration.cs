using HomeUtilities.Core.PdfBills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class PdfBillLayoutConfiguration : IEntityTypeConfiguration<PdfBillLayout>
{
    public void Configure(EntityTypeBuilder<PdfBillLayout> builder)
    {
        builder.ToTable("PdfBillLayouts");
        builder.Property(l => l.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(l => l.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Property(l => l.Markers).HasJsonColumn();
        builder.Property(l => l.Fields).HasJsonColumn();
    }
}
