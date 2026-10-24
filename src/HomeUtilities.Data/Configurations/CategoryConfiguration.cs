using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.Property(c => c.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.Property(c => c.Color).HasMaxLength(7);
        builder.Property(c => c.Icon).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(c => c.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.Ignore(c => c.IsBuiltIn);
        builder.HasIndex(c => c.SystemKey).IsUnique().HasFilter("\"SystemKey\" IS NOT NULL AND \"IsDeleted\" = 0");
    }
}
