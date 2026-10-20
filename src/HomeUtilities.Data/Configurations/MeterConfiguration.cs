using HomeUtilities.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class MeterConfiguration :
    IEntityTypeConfiguration<Meter>,
    IEntityTypeConfiguration<MeterRegister>,
    IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<Meter> builder)
    {
        builder.ToTable("Meters");
        builder.Property(m => m.SerialNumber).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(m => m.Location).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(m => m.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasReference<Meter, Property>(m => m.PropertyId);
        builder.HasReference<Meter, Meter>(m => m.ReplacedMeterId);
        builder.HasMany(m => m.Registers).WithOne().HasForeignKey(r => r.MeterId).OnDelete(DeleteBehavior.ClientCascade);
        builder.HasIndex(m => m.PropertyId);
    }

    public void Configure(EntityTypeBuilder<MeterRegister> builder)
    {
        builder.ToTable("MeterRegisters");
        builder.Property(r => r.Name).HasMaxLength(ConfigurationExtensions.NameLength);
        builder.HasMany(r => r.Readings).WithOne().HasForeignKey(r => r.RegisterId).OnDelete(DeleteBehavior.ClientCascade);
    }

    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("MeterReadings");
        builder.Property(r => r.Note).HasMaxLength(ConfigurationExtensions.NotesLength);
        builder.HasIndex(r => new { r.RegisterId, r.Date });
    }
}
