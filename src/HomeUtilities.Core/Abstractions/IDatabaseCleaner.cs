namespace HomeUtilities.Core.Abstractions;

/// <summary>What a database clean-up would remove (T-037).</summary>
/// <param name="DeletedRecords">Soft-deleted rows (<c>IsDeleted = 1</c>) in all data tables.</param>
/// <param name="OrphanFiles">Files in the attachments folder that belong to no live attachment row.</param>
/// <param name="OrphanFileBytes">Total size of those files.</param>
public sealed record CleanUpPreview(int DeletedRecords, int OrphanFiles, long OrphanFileBytes)
{
    /// <summary>True if there is nothing to remove.</summary>
    public bool IsEmpty => DeletedRecords == 0 && OrphanFiles == 0;
}

/// <summary>The outcome of a database clean-up (T-037).</summary>
/// <param name="RecordsRemoved">Rows deleted for good.</param>
/// <param name="FilesRemoved">Attachment files deleted.</param>
/// <param name="FileBytesRemoved">Total size of the deleted files.</param>
/// <param name="DatabaseBytesBefore">Size of the database file before the clean-up.</param>
/// <param name="DatabaseBytesAfter">Size of the database file after the clean-up (after <c>VACUUM</c>).</param>
/// <param name="KeptRecords">
/// Deleted rows that were <b>kept</b> because a live row still references them (a bug elsewhere); one text per table for the
/// error log. Empty in the normal case.
/// </param>
/// <param name="CompactionError">Why <c>VACUUM</c> could not run (the rows are removed all the same), or null.</param>
public sealed record CleanUpResult(
    int RecordsRemoved,
    int FilesRemoved,
    long FileBytesRemoved,
    long DatabaseBytesBefore,
    long DatabaseBytesAfter,
    IReadOnlyList<string> KeptRecords,
    string? CompactionError = null);

/// <summary>
/// "Clean up the database" (T-037): removes the soft-deleted rows of every data table for good, deletes the attachment files
/// that belong to no live attachment, and compacts the database. The backup copy before it and the sync consequences are the
/// caller's business.
/// </summary>
public interface IDatabaseCleaner
{
    /// <summary>Counts what <see cref="CleanAsync"/> would remove; changes nothing.</summary>
    Task<CleanUpPreview> PreviewAsync(CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes every <c>IsDeleted = 1</c> row in one transaction (children before parents; a row that a live row still
    /// references is kept and reported), then deletes the orphan attachment files and compacts the database.
    /// </summary>
    Task<CleanUpResult> CleanAsync(CancellationToken ct = default);
}
