using HomeUtilities.Core.Backup;

namespace HomeUtilities.Core.Abstractions;

/// <summary>The automatic backup copies in the app's data folder: creates, lists, opens and prunes them.</summary>
public interface IAutoBackupStorage
{
    /// <summary>How many of the newest copies are kept when old copies are pruned (the previous 3 months are kept in addition).</summary>
    int KeepCopies { get; set; }

    /// <summary>True if the database file exists, i.e. there is something to back up.</summary>
    bool DatabaseExists { get; }

    /// <summary>Raised after every attempt to make a copy, whoever asked for it (the app, or the database initializer).</summary>
    event EventHandler<AutoBackupResult>? Completed;

    /// <summary>
    /// Makes one copy with the same zip format as the manual backup and prunes old copies. Never throws and never runs two
    /// copies at once; a failure is returned (and raised through <see cref="Completed"/>).
    /// </summary>
    Task<AutoBackupResult> CreateAsync(AutoBackupReason reason, CancellationToken ct = default);

    /// <summary>The copies on this device, newest first (includes a copy restored by the Android system backup).</summary>
    IReadOnlyList<AutoBackupInfo> List();

    /// <summary>Opens a copy for reading (seekable); null if it no longer exists.</summary>
    Stream? Open(string name);
}
