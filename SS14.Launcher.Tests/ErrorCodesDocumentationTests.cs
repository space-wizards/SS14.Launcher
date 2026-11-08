using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SS14.Launcher.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ErrorCodesDocumentationTests
{
    private static string DocumentPath([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile)!;
        return Path.GetFullPath(Path.Combine(dir, "..", "ERROR_CODES.md"));
    }

    private static HashSet<string> CatalogCodes()
    {
        return typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ErrorCode))
            .Select(f => ((ErrorCode)f.GetValue(null)!).ToString())
            .ToHashSet();
    }

    private static HashSet<string> DocumentedCodes()
    {
        var path = DocumentPath();
        Assert.That(File.Exists(path), $"ERROR_CODES.md was not found at {path}");

        var row = new Regex(@"^\|\s*`(SS14L-[A-Z]+-\d{3})`", RegexOptions.Multiline);

        return row.Matches(File.ReadAllText(path))
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
    }

    [Test]
    public void EveryCodeIsDocumented()
    {
        var missing = CatalogCodes().Except(DocumentedCodes()).Order().ToList();

        Assert.That(missing, Is.Empty, "Codes missing from ERROR_CODES.md");
    }

    [Test]
    public void EveryDocumentedCodeExists()
    {
        var extra = DocumentedCodes().Except(CatalogCodes()).Order().ToList();

        Assert.That(extra, Is.Empty, "Codes in ERROR_CODES.md that are not in ErrorCodes.cs");
    }
}
