namespace HomeUtilities.Core.Backup;

/// <summary>Why a file cannot be used as a backup.</summary>
public enum BackupError
{
    /// <summary>The file is not a backup of this app (not a zip, no manifest, no database, damaged, unknown layout).</summary>
    NotABackup,

    /// <summary>The backup was made by a newer version of the app than this one.</summary>
    NewerVersion,
}

/// <summary>Result of checking a backup file: either its manifest or the reason it was refused.</summary>
public sealed record BackupValidation(BackupManifest? Manifest, BackupError? Error)
{
    /// <summary>True if the file is a usable backup.</summary>
    public bool IsValid => Manifest is not null;

    /// <summary>A valid result.</summary>
    public static BackupValidation Valid(BackupManifest manifest) => new(manifest, null);

    /// <summary>A refusal.</summary>
    public static BackupValidation Invalid(BackupError error) => new(null, error);
}
