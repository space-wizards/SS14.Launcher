using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SS14.Launcher.Tests;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ErrorCodesTests
{
    private static readonly string[] Areas = ["NET", "UPD", "AUTH", "CLI", "DB", "IO", "GEN"];

    private static IReadOnlyList<(string Name, ErrorCode Code)> GetAll()
    {
        return typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ErrorCode))
            .Select(f => (f.Name, (ErrorCode)f.GetValue(null)!))
            .ToList();
    }

    [Test]
    public void CatalogIsNotEmpty()
    {
        Assert.That(GetAll(), Is.Not.Empty);
    }

    [Test]
    public void CodesAreUnique()
    {
        var duplicates = GetAll()
            .GroupBy(c => c.Code)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(c => c.Name))}")
            .ToList();

        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void CodesAreWellFormed()
    {
        var format = new Regex(@"^SS14L-[A-Z]+-\d{3}$");

        foreach (var (name, code) in GetAll())
        {
            Assert.That(Areas, Does.Contain(code.Area), $"{name} uses an unknown area");
            // If we ever get more than 999 errors on an area, you are required to pay for my therapy.
            Assert.That(code.Number, Is.InRange(1, 999), $"{name} has a number out of range");
            Assert.That(format.IsMatch(code.ToString()), $"{name} is formatted as {code}");
        }
    }

    [Test]
    public void NumbersAreContiguousPerArea()
    {
        // I told you not to remove errors!!!!!!! Hiss
        // Also stops you from doing a funny like 067 without all the error codes that come prior.
        foreach (var group in GetAll().GroupBy(c => c.Code.Area))
        {
            var numbers = group.Select(c => c.Code.Number).Order().ToList();

            Assert.That(numbers, Is.EqualTo(Enumerable.Range(1, numbers.Count)),
                $"Numbers in area {group.Key} are not contiguous from 1");
        }
    }

    [Test]
    public void ToStringUsesPrefixAreaAndPaddedNumber()
    {
        Assert.That(new ErrorCode("UPD", 4).ToString(), Is.EqualTo("SS14L-UPD-004"));
        Assert.That(new ErrorCode("NET", 12).ToString(), Is.EqualTo("SS14L-NET-012"));
    }
}
