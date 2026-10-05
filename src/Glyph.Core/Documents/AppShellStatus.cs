namespace Glyph.Core.Documents;

/// <summary>
/// Main window / shell status strings (open, clipboard, file ops, prefs, session).
/// </summary>
public static class AppShellStatus
{
    public const string RecentFilesCleared = "Recent files cleared.";
    public const string OpenedPathFromClipboard = "Opened path from clipboard.";
    public const string ClipboardNoImageOrPath =
        "Clipboard has no image or openable file path.";
    public const string PasteFailed = "Paste failed.";
    public const string OpenDocumentToToggleToolbar =
        "Open a document to toggle the toolbar.";
    public const string ClipboardNoImage = "Clipboard does not contain an image.";
    public const string CouldNotOpenClipboardImage = "Could not open clipboard image.";
    public const string CouldNotCreateClipboardImage =
        "Could not create image from clipboard.";
    public const string SaveFirstToDuplicate = "Save the document first to duplicate it.";
    public const string SaveFirstToRename = "Save the document first to rename it.";
    public const string SaveFirstToMove = "Save the document first to move it.";
    public const string InvalidFileName = "Invalid file name.";
    public const string NameUnchanged = "Name unchanged.";
    public const string FileNameExists = "A file with that name already exists.";
    public const string MoveCancelled = "Move cancelled.";
    public const string AlreadyInFolder = "Already in that folder.";
    public const string DestinationReadOnly = "Destination file is read-only.";
    public const string FileNotFound = "File not found.";
    public const string UnsupportedFileType = "Unsupported file type.";
    public const string FailedToOpenFile = "Failed to open file.";
    public const string FailedToOpenImage = "Failed to open image.";
    public const string SessionRestoreFailed = "Session restore failed.";
    public const string SavedSignaturesCleared = "Saved signatures cleared.";
    public const string PreferencesSaved = "Preferences saved.";
    public const string OpenSavedForSnapshots =
        "Open a saved document to manage version snapshots.";
    public const string SnapshotMissing = "Snapshot file is missing.";
    public const string MoveToMonitorFailedPrefix = "Move to monitor failed: ";
    public const string DuplicateFailedPrefix = "Duplicate failed: ";
    public const string RenameFailedPrefix = "Rename failed: ";
    public const string MoveFailedPrefix = "Move failed: ";

    public static string FormatReady(string firstEntryPoint) =>
        $"Ready — {firstEntryPoint} or drop files here";

    public static string FormatCreatedClipboardImage(string fileName) =>
        $"Created image from clipboard: {fileName}";

    public static string FormatDuplicatedAs(string fileName) =>
        "Duplicated as " + fileName;

    public static string FormatRenamedTo(string newName) =>
        "Renamed to " + newName;

    public static string FormatMovedTo(string folderPath) =>
        "Moved to " + folderPath;

    public static string FormatOpenedInNewWindow(string fileName) =>
        "Opened in a new window: " + fileName;

    public static string FormatOpenedImage(string displayName) =>
        $"Opened image: {displayName}";

    public static string FormatOpenedPdf(string displayName) =>
        $"Opened PDF: {displayName}";

    public static string FormatTheme(string preference) =>
        $"Theme: {preference}";

    public static string FormatRestoringTabs(int count) =>
        $"Restoring {count} tab(s)…";

    public static string FormatRestoredTabs(int count) =>
        $"Restored {count} tab(s) from previous session.";

    public static string FormatOpenedSnapshotCopy(string fileName) =>
        "Opened snapshot copy: " + fileName;

    public static string FormatDeletedSnapshot(string whenLocal) =>
        "Deleted snapshot from " + whenLocal;

    public static string FormatRestoredSnapshot(string whenLocal) =>
        "Restored snapshot from " + whenLocal;

    public const string RenameDialogTitle = "Rename";
    public const string RenameButton = "Rename";
    public const string NewFileNameHeader = "New file name";
    public const string ReplaceExistingTitle = "Replace existing file?";
    public const string VersionSnapshotsTitlePrefix = "Version snapshots — ";
    public const string RestoreSnapshotTitle = "Restore snapshot?";
    public const string RestoreSnapshotBody =
        "Replace the current file on disk with this snapshot? A new snapshot of the current file will be kept first when snapshots are enabled.";
    public const string ReplaceButton = "Replace";
    public const string OpenAsCopyButton = "Open as copy";
    public const string RestoreButton = "Restore";
    public const string DeleteButton = "Delete";

    public static string FormatReplaceExistingBody(string fileName) =>
        $"“{fileName}” already exists in the destination folder.";

    public static string FormatVersionSnapshotsTitle(string displayName) =>
        VersionSnapshotsTitlePrefix + displayName;
    public const string Ready = "Ready";
    public const string OpenedFileFromClipboard = "Opened file from clipboard.";
    public const string NoVersionSnapshotsYet =
        "No version snapshots yet — they are created on Save.";
    public const string NoVersionSnapshotsDisabled =
        "No snapshots. Enable “Keep local version snapshots on Save” in Preferences.";

    public static string FormatFailed(string prefix, string message) =>
        prefix + message;

    public static string FormatOpenedFilesFromClipboard(int count) =>
        $"Opened {count} files from clipboard.";

    public static string FormatActive(string displayName) =>
        $"Active: {displayName}";
}
