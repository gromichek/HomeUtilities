using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>, IEntityTypeConfiguration<ContractMeter>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");
        builder.Property(c => c.Title).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(c => c.CustomerNumber).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(c => c.ContractNumber).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(c => c.PortalLogin).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(c => c.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.ComplexProperty(c => c.PaymentDetails, b => b.HasPaymentDetailColumns());
        builder.Property(c => c.Links).HasJsonColumn();
        builder.Property(c => c.CustomFields).HasJsonColumn();

        builder.HasReference<Contract, Property>(c => c.PropertyId);
        builder.HasReference<Contract, Supplier>(c => c.SupplierId);
        builder.HasReference<Contract, Category>(c => c.CategoryId);
        builder.HasIndex(c => c.PropertyId);
    }

    public void Configure(EntityTypeBuilder<ContractMeter> builder)
    {
        builder.ToTable("ContractMeters");
        builder.HasReference<ContractMeter, Contract>(c => c.ContractId);
        builder.HasReference<ContractMeter, Meter>(c => c.MeterId);
    }
}
