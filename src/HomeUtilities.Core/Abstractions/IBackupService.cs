using HomeUtilities.Core.Backup;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Creates backup files and restores them. A restore is staged first and applied at the next start.</summary>
public interface IBackupService
{
    /// <summary>File name for a backup made now: <c>HomeUtilities-backup-YYYY-MM-DD-HHmm.zip</c> (local time).</summary>
    string SuggestFileName();

    /// <summary>Writes a backup (manifest, consistent database copy, attachment files) as a zip to the stream.</summary>
    Task<BackupManifest> CreateAsync(Stream output, CancellationToken ct = default);

    /// <summary>Checks that the (seekable) stream holds a usable backup; never throws for a bad file.</summary>
    Task<BackupValidation> ValidateAsync(Stream backup, CancellationToken ct = default);

    /// <summary>Validates the (seekable) stream and, if it is a backup, keeps it for the next start.</summary>
    Task<BackupValidation> StageRestoreAsync(Stream backup, CancellationToken ct = default);

    /// <summary>Marks all data to be deleted at the next start (debug builds only).</summary>
    void StageWipe();

    /// <summary>
    /// Startup step, before the database is opened: applies a staged restore or wipe. Returns null if nothing was staged.
    /// A failure keeps the current data and is reported in the outcome; it never throws.
    /// </summary>
    RestoreOutcome? ApplyPendingRestore();
}
