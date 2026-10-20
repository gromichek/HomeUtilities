using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data;

/// <summary>Inserts the reference data that must exist from the start. Idempotent and never overwrites user edits.</summary>
public static class DataSeeder
{
    /// <summary>
    /// Adds the Latvian VAT rates and the built-in categories that are missing. A row that exists, even
    /// soft-deleted, is left alone: the user may have deleted or edited it on purpose.
    /// </summary>
    public static async Task SeedAsync(HomeUtilitiesDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);

        var vatCodes = await db.VatRates.IgnoreQueryFilters().Select(v => v.Code).ToListAsync(ct);
        foreach (var rate in LatvianVatDefaults.Create().Where(r => !vatCodes.Contains(r.Code)))
        {
            db.VatRates.Add(rate);
        }

        var systemKeys = await db.Categories.IgnoreQueryFilters()
            .Where(c => c.SystemKey != null)
            .Select(c => c.SystemKey)
            .ToListAsync(ct);
        foreach (var category in BuiltInCategories.Create().Where(c => !systemKeys.Contains(c.SystemKey)))
        {
            db.Categories.Add(category);
        }

        await db.SaveChangesAsync(ct);
    }
}
