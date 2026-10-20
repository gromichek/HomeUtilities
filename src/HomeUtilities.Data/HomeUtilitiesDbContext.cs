using System.Linq.Expressions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Data.Conversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HomeUtilities.Data;

/// <summary>EF Core context of the whole domain. Mappings live in <c>Configurations/</c>.</summary>
/// <remarks>
/// Register a <see cref="SyncEntityInterceptor"/> in the options to get timestamps and soft delete.
/// </remarks>
public class HomeUtilitiesDbContext(DbContextOptions<HomeUtilitiesDbContext> options) : DbContext(options)
{
    /// <summary>Name of the query filter that hides soft-deleted rows.</summary>
    public const string SoftDeleteFilter = "SoftDelete";

    /// <summary>When true the interceptor leaves the given timestamps alone (used when applying synced rows).</summary>
    public bool PreserveTimestamps { get; set; }

    public DbSet<Property> Properties => Set<Property>();

    public DbSet<PropertyParameter> PropertyParameters => Set<PropertyParameter>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<ContractMeter> ContractMeters => Set<ContractMeter>();

    public DbSet<Meter> Meters => Set<Meter>();

    public DbSet<MeterRegister> MeterRegisters => Set<MeterRegister>();

    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

    public DbSet<VatRate> VatRates => Set<VatRate>();

    public DbSet<VatRateVersion> VatRateVersions => Set<VatRateVersion>();

    public DbSet<TariffComponent> TariffComponents => Set<TariffComponent>();

    public DbSet<TariffRate> TariffRates => Set<TariffRate>();

    public DbSet<Bill> Bills => Set<Bill>();

    public DbSet<BillLine> BillLines => Set<BillLine>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToTicksConverter>();
        configurationBuilder.Properties<YearMonth>().HaveConversion<YearMonthToIntConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Items of JSON columns are plain values, not entities.
        modelBuilder.Ignore<Link>();
        modelBuilder.Ignore<CustomFieldDefinition>();
        modelBuilder.Ignore<CustomFieldValue>();
        modelBuilder.Ignore<TariffTier>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HomeUtilitiesDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.ClrType.IsAssignableTo(typeof(SyncEntity))))
        {
            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.Property(nameof(SyncEntity.Id)).ValueGeneratedNever();
            builder.HasIndex(nameof(SyncEntity.UpdatedAt));
            builder.HasQueryFilter(SoftDeleteFilter, NotDeleted(entityType));
        }
    }

    private static LambdaExpression NotDeleted(IReadOnlyEntityType entityType)
    {
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var isDeleted = Expression.Property(parameter, nameof(SyncEntity.IsDeleted));
        return Expression.Lambda(Expression.Not(isDeleted), parameter);
    }
}
