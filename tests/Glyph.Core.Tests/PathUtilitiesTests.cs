using FluentAssertions;
using Glyph.Core.IO;

namespace Glyph.Core.Tests;

public class PathUtilitiesTests
{
    [Fact]
    public void NormalizeOpenPath_trims_quotes_and_resolves_relative()
    {
        var dir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var file = Path.Combine(dir, "glyph-path-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(file, "x");
        try
        {
            var quoted = "\"" + file + "\"";
            var normalized = PathUtilities.NormalizeOpenPath(quoted);
            normalized.Should().Be(Path.GetFullPath(file));
            PathUtilities.FileExists(quoted).Should().BeTrue();
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void NormalizeOpenPath_accepts_unicode_filename()
    {
        var dir = Path.GetTempPath();
        var file = Path.Combine(dir, "документ-照片-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllText(file, "x");
        try
        {
            PathUtilities.FileExists(file).Should().BeTrue();
            PathUtilities.NormalizeOpenPath(file).Should().Contain("документ");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void NormalizeOpenPath_leaves_long_path_prefix()
    {
        var prefixed = @"\\?\C:\very\long\path\file.pdf";
        PathUtilities.NormalizeOpenPath(prefixed).Should().Be(prefixed);
    }

    [Fact]
    public void IsPathReadOnly_detects_attribute()
    {
        var file = Path.Combine(Path.GetTempPath(), "glyph-ro-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(file, "x");
        try
        {
            PathUtilities.IsPathReadOnly(file).Should().BeFalse();
            File.SetAttributes(file, FileAttributes.ReadOnly);
            PathUtilities.IsPathReadOnly(file).Should().BeTrue();
            File.SetAttributes(file, FileAttributes.Normal);
            PathUtilities.IsPathReadOnly("missing-" + Guid.NewGuid().ToString("N") + ".tmp").Should().BeFalse();
            PathUtilities.IsPathReadOnly("").Should().BeFalse();
        }
        finally
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            catch
            {
                // best-effort cleanup
            }

            File.Delete(file);
        }
    }
}
