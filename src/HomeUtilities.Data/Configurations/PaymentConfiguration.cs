using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>, IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.Property(p => p.Reference).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Property(p => p.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasReference<Payment, Contract>(p => p.ContractId);
        builder.HasReference<Payment, Property>(p => p.PropertyId);
        builder.HasMany(p => p.Allocations).WithOne().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.ClientCascade);
    }

    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations");
        builder.Property(a => a.Note).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasReference<PaymentAllocation, Bill>(a => a.BillId);
        builder.HasIndex(a => a.BillId);
    }
}
