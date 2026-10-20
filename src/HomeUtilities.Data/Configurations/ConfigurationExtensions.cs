using HomeUtilities.Core.Payments;
using HomeUtilities.Data.Conversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeUtilities.Data.Configurations;

internal static class ConfigurationExtensions
{
    public const int NameLength = 100;
    public const int NotesLength = 2000;
    public const int ShortTextLength = 200;
    public const int CodeLength = 50;

    /// <summary>Stores the property as a JSON text column that detects in-place changes.</summary>
    public static PropertyBuilder<T> HasJsonColumn<T>(this PropertyBuilder<T> property)
        where T : class? =>
        property.HasConversion(JsonColumn.Converter<T>(), JsonColumn.Comparer<T>()).HasColumnType("TEXT");

    /// <summary>Maps an optional <see cref="PaymentDetails"/> to columns prefixed <c>Pay_</c>.</summary>
    public static ComplexPropertyBuilder<PaymentDetails> HasPaymentDetailColumns(this ComplexPropertyBuilder<PaymentDetails> builder)
    {
        // All six fields are optional, so a shadow flag column tells "no details" (null) from "empty details".
        builder.HasDiscriminator<int>("Present").HasValue(1);
        builder.Property<int>("Present").HasColumnName("Pay_Present");
        builder.Property(p => p.RecipientName).HasColumnName("Pay_RecipientName").HasMaxLength(ShortTextLength);
        builder.Property(p => p.RecipientRegistrationNumber).HasColumnName("Pay_RecipientRegistrationNumber").HasMaxLength(CodeLength);
        builder.Property(p => p.Iban).HasColumnName("Pay_Iban").HasMaxLength(34);
        builder.Property(p => p.Bic).HasColumnName("Pay_Bic").HasMaxLength(CodeLength);
        builder.Property(p => p.BankName).HasColumnName("Pay_BankName").HasMaxLength(ShortTextLength);
        builder.Property(p => p.PurposeTemplate).HasColumnName("Pay_PurposeTemplate").HasMaxLength(NotesLength);
        return builder;
    }

    /// <summary>Adds a FK without a navigation on the principal side; the database never cascades deletes.</summary>
    public static void HasReference<TDependent, TPrincipal>(
        this EntityTypeBuilder<TDependent> builder,
        System.Linq.Expressions.Expression<Func<TDependent, object?>> foreignKey)
        where TDependent : class
        where TPrincipal : class =>
        builder.HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(DeleteBehavior.Restrict);
}
