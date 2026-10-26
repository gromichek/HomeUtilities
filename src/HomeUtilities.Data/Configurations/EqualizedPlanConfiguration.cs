using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class EqualizedPlanConfiguration : IEntityTypeConfiguration<EqualizedPlan>
{
    public void Configure(EntityTypeBuilder<EqualizedPlan> builder)
    {
        builder.ToTable("EqualizedPlans");
        builder.Property(p => p.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasReference<EqualizedPlan, Contract>(p => p.ContractId);
        builder.HasIndex(p => new { p.ContractId, p.ValidFrom });
    }
}
