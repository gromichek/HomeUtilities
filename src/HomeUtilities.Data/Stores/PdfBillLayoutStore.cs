using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.PdfBills;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Stores;

/// <summary>EF Core implementation of <see cref="IPdfBillLayoutStore"/>.</summary>
public sealed class PdfBillLayoutStore(IDbContextFactory<HomeUtilitiesDbContext> factory)
    : StoreBase<PdfBillLayout>(factory), IPdfBillLayoutStore
{
    /// <inheritdoc />
    protected override IQueryable<PdfBillLayout> Include(IQueryable<PdfBillLayout> query) => query;

    /// <inheritdoc />
    protected override IQueryable<PdfBillLayout> Order(IQueryable<PdfBillLayout> query) =>
        query.OrderBy(l => l.Name).ThenBy(l => l.CreatedAt);
}
