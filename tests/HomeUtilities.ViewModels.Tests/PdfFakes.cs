using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.ViewModels.Tests;

internal sealed class FakePdfLayoutStore : IPdfBillLayoutStore
{
    public List<PdfBillLayout> Stored { get; } = [];

    public Task<PdfBillLayout?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(l => l.Id == id));

    public Task<IReadOnlyList<PdfBillLayout>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PdfBillLayout>>([.. Stored.OrderBy(l => l.Name)]);

    public Task SaveAsync(PdfBillLayout aggregate, CancellationToken ct = default)
    {
        Stored.RemoveAll(l => l.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Stored.RemoveAll(l => l.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakePdfTextReader : IPdfTextReader
{
    /// <summary>The text every read returns.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>When set, every read fails with it.</summary>
    public ValidationError? Error { get; set; }

    public int Calls { get; private set; }

    public Task<PdfTextResult> ReadAsync(Stream pdf, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(Error is null ? new PdfTextResult(Text, null) : new PdfTextResult(string.Empty, Error));
    }
}
