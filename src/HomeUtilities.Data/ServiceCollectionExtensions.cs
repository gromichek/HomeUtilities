using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Data.Sample;
using HomeUtilities.Data.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeUtilities.Data;

/// <summary>Registration of the storage layer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite context factory, all stores, the charge input loader and the database initializer.
    /// Needs an <see cref="IClock"/> in the container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Full path of the SQLite file; its folder is created if missing.</param>
    /// <param name="attachmentsFolder">Folder of the attachment files; created when the first file is stored.</param>
    public static IServiceCollection AddHomeUtilitiesData(this IServiceCollection services, string databasePath, string attachmentsFolder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentsFolder);

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        services.AddDbContextFactory<HomeUtilitiesDbContext>((provider, options) => options
            .UseSqlite(connectionString)
            .AddInterceptors(new SyncEntityInterceptor(provider.GetRequiredService<IClock>())));

        services.AddSingleton<IPropertyStore, PropertyStore>();
        services.AddSingleton<ICategoryStore, CategoryStore>();
        services.AddSingleton<ISupplierStore, SupplierStore>();
        services.AddSingleton<IVatRateStore, VatRateStore>();
        services.AddSingleton<IContractStore, ContractStore>();
        services.AddSingleton<IMeterStore, MeterStore>();
        services.AddSingleton<ITariffStore, TariffStore>();
        services.AddSingleton<IEqualizedPlanStore, EqualizedPlanStore>();
        services.AddSingleton<IBillStore, BillStore>();
        services.AddSingleton<IBillTemplateStore, BillTemplateStore>();
        services.AddSingleton<IPaymentStore, PaymentStore>();
        services.AddSingleton<IImportProfileStore, ImportProfileStore>();
        services.AddSingleton<IImportBatchStore, ImportBatchStore>();
        services.AddSingleton<IAttachmentStore>(provider => new AttachmentStore(
            provider.GetRequiredService<IDbContextFactory<HomeUtilitiesDbContext>>(),
            attachmentsFolder,
            provider.GetService<IImageProcessor>()));
        services.AddSingleton<IChargeInputLoader, ChargeInputLoader>();
        services.AddSingleton<IOverviewDataLoader, OverviewDataLoader>();
        services.AddSingleton<IReportDataLoader, ReportDataLoader>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<ISampleDataGenerator, SampleDataGenerator>();
        return services;
    }
}
