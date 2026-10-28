using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class BillTemplateConfiguration : IEntityTypeConfiguration<BillTemplate>
{
    public void Configure(EntityTypeBuilder<BillTemplate> builder)
    {
        builder.ToTable("BillTemplates");
        builder.Property(t => t.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(t => t.Title).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(t => t.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Property(t => t.Lines).HasJsonColumn();
        builder.Property(t => t.CustomFields).HasJsonColumn();
        builder.Property(t => t.Schedule).HasJsonColumn();

        builder.HasReference<BillTemplate, Property>(t => t.PropertyId);
        builder.HasReference<BillTemplate, Category>(t => t.CategoryId);
        builder.HasReference<BillTemplate, Contract>(t => t.ContractId);
        builder.HasReference<BillTemplate, Supplier>(t => t.SupplierId);
    }
}
