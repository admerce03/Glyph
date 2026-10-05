namespace Glyph.Core.IO;

/// <summary>
/// Same-folder duplicate / rename path helpers (F01-15 / F01-19).
/// </summary>
public static class DocumentFileNamePolicy
{
    public static string SuggestDuplicatePath(string sourcePath, Func<string, bool>? fileExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        fileExists ??= File.Exists;

        var dir = Path.GetDirectoryName(sourcePath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = Path.GetExtension(sourcePath);
        var candidate = Path.Combine(dir, name + " copy" + ext);
        var n = 2;
        while (fileExists(candidate))
        {
            candidate = Path.Combine(dir, $"{name} copy {n}{ext}");
            n++;
        }

        return candidate;
    }

    public static bool IsValidFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        return !trimmed.Contains('/') && !trimmed.Contains('\\');
    }

    public static RenamePathResult EvaluateRename(string currentPath, string proposedName, Func<string, bool>? fileExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        fileExists ??= File.Exists;

        var currentName = Path.GetFileName(currentPath);
        var newName = (proposedName ?? string.Empty).Trim();
        if (!IsValidFileName(newName))
        {
            return RenamePathResult.Invalid;
        }

        if (string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
        {
            return RenamePathResult.Unchanged;
        }

        var dir = Path.GetDirectoryName(currentPath) ?? ".";
        var dest = Path.Combine(dir, newName);
        if (fileExists(dest))
        {
            return RenamePathResult.Conflict;
        }

        return RenamePathResult.Ok(dest);
    }
}

public readonly record struct RenamePathResult(RenamePathStatus Status, string? DestinationPath)
{
    public static RenamePathResult Invalid => new(RenamePathStatus.Invalid, null);
    public static RenamePathResult Unchanged => new(RenamePathStatus.Unchanged, null);
    public static RenamePathResult Conflict => new(RenamePathStatus.Conflict, null);
    public static RenamePathResult Ok(string destination) => new(RenamePathStatus.Ok, destination);
}

public enum RenamePathStatus
{
    Ok,
    Invalid,
    Unchanged,
    Conflict,
}
