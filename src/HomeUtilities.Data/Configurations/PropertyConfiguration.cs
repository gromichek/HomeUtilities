using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>, IEntityTypeConfiguration<PropertyParameter>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");
        builder.Property(p => p.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(p => p.Address).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(p => p.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasMany(p => p.Parameters).WithOne().HasForeignKey(p => p.PropertyId).OnDelete(DeleteBehavior.ClientCascade);
    }

    public void Configure(EntityTypeBuilder<PropertyParameter> builder)
    {
        builder.ToTable("PropertyParameters");
        builder.Property(p => p.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(p => p.Note).HasMaxLength(ConfigurationExtensions.NotesLength);
    }
}
