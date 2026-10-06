using System.Text.RegularExpressions;
using FluentAssertions;

namespace Glyph.Core.Tests;

public class FeatureMatrixHonestyTests
{
    [Fact]
    public void Roadmap_matrix_audit_row_count_matches_feature_matrix()
    {
        var root = FindRepoRoot();
        var matrixPath = Path.Combine(root, "docs", "FEATURE_MATRIX.md");
        var roadmapPath = Path.Combine(root, "docs", "ROADMAP.md");
        File.Exists(matrixPath).Should().BeTrue(matrixPath);
        File.Exists(roadmapPath).Should().BeTrue(roadmapPath);

        var matrix = File.ReadAllText(matrixPath);
        var dataRows = Regex.Matches(matrix, @"^\| F\d{2}-\d{2} ", RegexOptions.Multiline).Count;
        var tested = Regex.Matches(matrix, @"^\| F\d{2}-\d{2} .*\| Tested \|", RegexOptions.Multiline).Count;
        var deferred = Regex.Matches(matrix, @"^\| F\d{2}-\d{2} .*\| Deferred \|", RegexOptions.Multiline).Count;
        var blocked = Regex.Matches(matrix, @"^\| F\d{2}-\d{2} .*\| Blocked \|", RegexOptions.Multiline).Count;

        dataRows.Should().Be(tested + deferred + blocked);
        blocked.Should().Be(0);

        var roadmap = File.ReadAllText(roadmapPath);
        var audit = Regex.Match(
            roadmap,
            @"Matrix audit[^\n]*?(\d+) rows — (\d+) Tested / (\d+) Deferred / (\d+) Blocked");
        audit.Success.Should().BeTrue("ROADMAP should include a Matrix audit line with row totals");
        int.Parse(audit.Groups[1].Value).Should().Be(dataRows);
        int.Parse(audit.Groups[2].Value).Should().Be(tested);
        int.Parse(audit.Groups[3].Value).Should().Be(deferred);
        int.Parse(audit.Groups[4].Value).Should().Be(blocked);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "FEATURE_MATRIX.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found");
    }
}
