using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>, IEntityTypeConfiguration<BillLine>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("Bills");
        builder.Property(b => b.Title).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(b => b.Number).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(b => b.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Property(b => b.CustomFields).HasJsonColumn();
        builder.Property(b => b.AttachmentIds).HasJsonColumn();
        builder.Property(b => b.BillQuantities).HasJsonColumn();
        builder.Property(b => b.CalculatedSnapshot).HasJsonColumn();

        builder.HasReference<Bill, Property>(b => b.PropertyId);
        builder.HasReference<Bill, Category>(b => b.CategoryId);
        builder.HasReference<Bill, Contract>(b => b.ContractId);
        builder.HasReference<Bill, Supplier>(b => b.SupplierId);
        builder.HasMany(b => b.Lines).WithOne().HasForeignKey(l => l.BillId).OnDelete(DeleteBehavior.ClientCascade);

        builder.HasIndex(b => new { b.ContractId, b.Period });
        builder.HasIndex(b => new { b.PropertyId, b.Period });
        builder.HasIndex(b => b.TemplateId);
    }

    public void Configure(EntityTypeBuilder<BillLine> builder)
    {
        builder.ToTable("BillLines");
        builder.Property(l => l.Description).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(l => l.Unit).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(l => l.Note).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Property(l => l.CustomFields).HasJsonColumn();
        builder.HasReference<BillLine, Category>(l => l.CategoryId);
    }
}
