using HomeUtilities.Core.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.Property(a => a.FileName).HasMaxLength(ConfigurationExtensions.ShortTextLength);
        builder.Property(a => a.ContentType).HasMaxLength(ConfigurationExtensions.CodeLength);
        builder.Property(a => a.Sha256).HasMaxLength(64);
        builder.Property(a => a.Notes).HasMaxLength(ConfigurationExtensions.NotesLength);

        // The owner is polymorphic (reading, bill, meter...), so there is no foreign key, only an index to find the files of a record.
        builder.HasIndex(a => new { a.OwnerKind, a.OwnerId });
    }
}
