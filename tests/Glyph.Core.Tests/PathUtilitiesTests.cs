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
}
