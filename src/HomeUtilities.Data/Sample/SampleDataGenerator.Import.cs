using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Data.Sample;

public sealed partial class SampleDataGenerator
{
    /// <summary>
    /// A saved import profile for a "waste" sheet (month, net, VAT, total, paid marker) so the Imports screen has something to
    /// show; the sample database has no finished import.
    /// </summary>
    private Task AddImportProfileAsync(Contract waste, CancellationToken ct) =>
        SaveAsync(
            new SavedImportProfile
            {
                Name = "Waste sheet (sample)",
                Profile = new ImportProfile
                {
                    SheetName = "Waste",
                    ContractId = waste.Id,
                    PeriodColumn = 1,
                    NetColumn = 2,
                    VatColumn = 3,
                    TotalColumn = 4,
                    PaidColumn = 5,
                    PaidDateColumn = 6,
                    NotesColumns = [7],
                },
            },
            importProfiles,
            ct);
}
