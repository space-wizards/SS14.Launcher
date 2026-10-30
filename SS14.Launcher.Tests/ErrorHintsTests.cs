using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Linguini.Syntax.Parser;
using NUnit.Framework;

namespace SS14.Launcher.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ErrorHintsTests
{
    private static string LocalePath(string fileName, [CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile)!;
        return Path.GetFullPath(Path.Combine(dir, "..", "SS14.Launcher", "Assets", "Locale", "en-US", fileName));
    }

    private static string ReadLocale(string fileName)
    {
        var path = LocalePath(fileName);
        Assert.That(File.Exists(path), $"The English locale file was not found at {path}");

        return File.ReadAllText(path);
    }

    private static HashSet<string> MessageIds(string fileName)
    {
        var id = new Regex(@"^([a-z][a-z0-9-]*)\s*=", RegexOptions.Multiline);

        return id.Matches(ReadLocale(fileName))
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
    }

    private static HashSet<string> HintKeys()
    {
        return MessageIds("error.ftl")
            .Where(id => Regex.IsMatch(id, @"^error-hint-[a-z]+-\d{3}$"))
            .ToHashSet();
    }

    [Test]
    public void ErrorStringsParseWithoutErrors()
    {
        using var reader = new StringReader(ReadLocale("error.ftl"));
        var resource = new LinguiniParser(reader).Parse(); // I love this name so much idk why its so funny to me...

        Assert.That(resource.Errors, Is.Empty);
    }

    [Test]
    public void NoStringIsDefinedInBothFiles()
    {
        var both = MessageIds("text.ftl").Intersect(MessageIds("error.ftl")).Order().ToList();

        Assert.That(both, Is.Empty, "String ids defined in both text.ftl and error.ftl");
    }

    [Test]
    public void AreaKeyNamesFollowTheArea()
    {
        Assert.That(ErrorHints.GetAreaKey(new ErrorCode("NET", 3)), Is.EqualTo("error-hint-net"));
        Assert.That(ErrorHints.GetAreaKey(new ErrorCode("AUTH", 9)), Is.EqualTo("error-hint-auth"));
    }

    [Test]
    public void GenericHintsAreForRealAreas()
    {
        var areas = AllCodes().Select(c => ErrorHints.GetAreaKey(c)).ToHashSet();

        var stray = MessageIds("error.ftl")
            .Where(id => Regex.IsMatch(id, @"^error-hint-[a-z]+$"))
            .Where(id => !areas.Contains(id))
            .Order()
            .ToList();

        Assert.That(stray, Is.Empty, "Generic error-hint strings that don't match any error area");
    }

    private static List<ErrorCode> AllCodes()
    {
        return typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ErrorCode))
            .Select(f => (ErrorCode)f.GetValue(null)!)
            .ToList();
    }

    [Test]
    public void KeyNamesFollowTheCode()
    {
        Assert.That(ErrorHints.GetKey(new ErrorCode("IO", 4)), Is.EqualTo("error-hint-io-004"));
        Assert.That(ErrorHints.GetKey(new ErrorCode("NET", 14)), Is.EqualTo("error-hint-net-014"));
    }

    [Test]
    public void EveryIoCodeHasAHint()
    {
        var keys = HintKeys();

        var missing = AllCodes()
            .Where(c => c.Area == "IO")
            .Select(ErrorHints.GetKey)
            .Where(k => !keys.Contains(k))
            .Order()
            .ToList();

        // Forcing you to write io hints, get fucked
        Assert.That(missing, Is.Empty, "IO codes without an error-hint string in the English locale file");
    }

    [Test]
    public void EveryHintIsForARealCode()
    {
        var known = AllCodes().Select(ErrorHints.GetKey).ToHashSet();

        var stray = HintKeys().Where(k => !known.Contains(k)).Order().ToList();

        Assert.That(stray, Is.Empty, "error-hint strings that don't match any error code");
    }
}
