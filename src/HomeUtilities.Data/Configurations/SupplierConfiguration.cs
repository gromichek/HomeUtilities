using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.Property(s => s.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(s => s.RegistrationNumber).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(s => s.Phone).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(s => s.Email).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(s => s.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.ComplexProperty(s => s.PaymentDetails, b => b.HasPaymentDetailColumns());
        builder.Property(s => s.Links).HasJsonColumn();
    }
}
