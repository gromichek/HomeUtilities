using HomeUtilities.Core.Import;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Store of saved import profiles.</summary>
public interface IImportProfileStore : IStore<SavedImportProfile>
{
}

/// <summary>What <see cref="IImportBatchStore.ApplyAsync"/> needs to run an import.</summary>
/// <param name="Plan">The dry run to carry out; rows with errors create nothing.</param>
/// <param name="FileName">Name of the workbook file.</param>
/// <param name="SheetName">Name of the sheet.</param>
/// <param name="ContractId">The target contract.</param>
/// <param name="ProfileId">The saved profile, if any.</param>
public sealed record ImportApplyRequest(ImportPlan Plan, string FileName, string SheetName, Guid ContractId, Guid? ProfileId);

/// <summary>How an undo ended.</summary>
public enum UndoImportOutcome
{
    /// <summary>Everything of the batch was soft-deleted.</summary>
    Done = 0,

    /// <summary>Some records were edited after the import; nothing was changed. Ask the owner and call again with confirmation.</summary>
    NeedsConfirmation = 1,

    /// <summary>The batch does not exist (or was already undone).</summary>
    NotFound = 2,
}

/// <summary>Result of <see cref="IImportBatchStore.UndoAsync"/>.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="EditedCount">Number of records edited after the import.</param>
public sealed record UndoImportResult(UndoImportOutcome Outcome, int EditedCount);

/// <summary>Store of import batches: runs an import in one transaction and undoes a whole batch.</summary>
public interface IImportBatchStore
{
    /// <summary>Non-deleted batches, newest first.</summary>
    Task<IReadOnlyList<ImportBatch>> ListAsync(CancellationToken ct = default);

    /// <summary>One batch, or null.</summary>
    Task<ImportBatch?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Stores the readings, bills, payments and allocations of the plan and the batch that ties them together, in one
    /// transaction: either everything is stored or nothing.
    /// </summary>
    Task<ImportBatch> ApplyAsync(ImportApplyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes the batch with its bills, their allocations, payments and readings in one transaction. When records were
    /// changed after the import and <paramref name="confirmEdited"/> is false, nothing is changed and
    /// <see cref="UndoImportOutcome.NeedsConfirmation"/> is returned.
    /// </summary>
    Task<UndoImportResult> UndoAsync(Guid batchId, bool confirmEdited, CancellationToken ct = default);
}
