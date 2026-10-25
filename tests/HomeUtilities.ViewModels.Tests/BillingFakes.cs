using System.Reflection;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Copies of stored graphs, like a real store returns: the caller may change them freely.</summary>
internal static class Copies
{
    private static readonly MethodInfo CloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static T Shallow<T>(T source)
        where T : class => (T)CloneMethod.Invoke(source, null)!;

    public static Bill Of(Bill bill)
    {
        var copy = Shallow(bill);
        copy.Lines = [.. bill.Lines.Where(l => !l.IsDeleted).Select(l =>
        {
            var line = Shallow(l);
            line.CustomFields = [.. l.CustomFields];
            return line;
        })];
        copy.CustomFields = [.. bill.CustomFields];
        copy.AttachmentIds = [.. bill.AttachmentIds];
        return copy;
    }

    public static Payment Of(Payment payment)
    {
        var copy = Shallow(payment);
        copy.Allocations = [.. payment.Allocations.Where(a => !a.IsDeleted).Select(Shallow)];
        return copy;
    }
}

internal sealed class FakeBillStore : IBillStore
{
    /// <summary>Every bill ever stored, deleted ones included (nothing is hard-deleted).</summary>
    public List<Bill> All { get; } = [];

    public List<Bill> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public IEnumerable<Bill> Stored => All.Where(b => !b.IsDeleted);

    public Task<IReadOnlyList<Bill>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Bill>>([.. Stored.Select(Copies.Of).OrderBy(b => b.Period)]);

    public Task<IReadOnlyList<Bill>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Bill>>([.. Stored.Where(b => b.ContractId == contractId).Select(Copies.Of).OrderBy(b => b.Period)]);

    public Task<IReadOnlyList<Bill>> ListByPropertyAsync(Guid propertyId, YearMonth? from, YearMonth? to, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Bill>>([.. Stored
            .Where(b => b.PropertyId == propertyId && (from is null || b.Period >= from) && (to is null || b.Period <= to))
            .Select(Copies.Of).OrderBy(b => b.Period)]);

    public Task<Bill?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(b => b.Id == id) is { } bill ? Copies.Of(bill) : null);

    public Task SaveAsync(Bill aggregate, CancellationToken ct = default)
    {
        Saved.Add(Copies.Of(aggregate));
        All.RemoveAll(b => b.Id == aggregate.Id);
        All.Add(Copies.Of(aggregate));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        foreach (var bill in All.Where(b => b.Id == id))
        {
            bill.IsDeleted = true;
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakePaymentStore : IPaymentStore
{
    /// <summary>Every payment ever stored, deleted ones (and their deleted allocations) included.</summary>
    public List<Payment> All { get; } = [];

    public List<Payment> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public IEnumerable<Payment> Stored => All.Where(p => !p.IsDeleted);

    /// <summary>Adds a ready payment (test arrangement).</summary>
    public void Add(Payment payment) => All.Add(payment);

    public Task<IReadOnlyList<Payment>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored.Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<IReadOnlyList<Payment>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored
            .Where(p => p.ContractId == contractId || p.Allocations.Any(a => !a.IsDeleted && BillContract(a.BillId) == contractId))
            .Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<IReadOnlyList<Payment>> ListByBillAsync(Guid billId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored
            .Where(p => p.Allocations.Any(a => !a.IsDeleted && a.BillId == billId))
            .Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<IReadOnlyList<Payment>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Payment>>([.. Stored.Where(p => p.PropertyId == propertyId).Select(Copies.Of).OrderBy(p => p.PaidOn)]);

    public Task<Payment?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(p => p.Id == id) is { } payment ? Copies.Of(payment) : null);

    public Task SaveAsync(Payment aggregate, CancellationToken ct = default)
    {
        Saved.Add(Copies.Of(aggregate));
        var stored = All.FirstOrDefault(p => p.Id == aggregate.Id);
        var copy = Copies.Of(aggregate);
        if (stored is not null)
        {
            // Allocations missing from the incoming graph are soft-deleted, like the real store does.
            var incoming = copy.Allocations.Select(a => a.Id).ToHashSet();
            foreach (var removed in stored.Allocations.Where(a => !incoming.Contains(a.Id)))
            {
                removed.IsDeleted = true;
                copy.Allocations.Add(removed);
            }
        }

        All.RemoveAll(p => p.Id == aggregate.Id);
        All.Add(copy);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        foreach (var payment in All.Where(p => p.Id == id))
        {
            payment.IsDeleted = true;
            foreach (var allocation in payment.Allocations)
            {
                allocation.IsDeleted = true;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Resolves the contract of a bill for the contract listing; set by the test kit.</summary>
    public Func<Guid, Guid?> BillContractResolver { get; set; } = _ => null;

    private Guid? BillContract(Guid billId) => BillContractResolver(billId);
}
