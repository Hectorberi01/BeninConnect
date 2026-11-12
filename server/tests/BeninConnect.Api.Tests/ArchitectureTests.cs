using System.Text.RegularExpressions;

namespace BeninConnect.Api.Tests;

/// <summary>
/// Frontières du monolithe modulaire (vérifiées sur le code source) : un module ne voit des autres que leurs Contracts,
/// et chaque module respecte la Clean Architecture (Domain ← Application ← Infrastructure / Api).
/// Shared.Domain et Shared.Application sont utilisables partout ; Shared.Infrastructure et Shared.Api seulement dans les couches du même nom.
/// </summary>
public sealed partial class ArchitectureTests
{
    private static readonly string[] Layers = ["Contracts", "Domain", "Application", "Infrastructure", "Api"];

    private static readonly string ApiRoot = FindApiRoot();

    private static string FindApiRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "server", "src", "BeninConnect.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("server/src/BeninConnect.Api introuvable.");
    }

    private static IEnumerable<string> Modules => Directory.GetDirectories(Path.Combine(ApiRoot, "Modules")).Select(Path.GetFileName)!;

    private static IEnumerable<(string File, string Reference)> References(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .SelectMany(f => ReferenceRegex().Matches(File.ReadAllText(f)).Select(m => (Path.GetRelativePath(ApiRoot, f), m.Value)));

    [GeneratedRegex(@"BeninConnect\.Modules\.\w+\.\w+|BeninConnect\.Shared\.\w+|Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore")]
    private static partial Regex ReferenceRegex();

    [Fact]
    public void Every_module_has_the_five_layers()
    {
        Assert.Equal(10, Modules.Count());
        var missing = Modules.SelectMany(m => Layers.Where(l => !Directory.Exists(Path.Combine(ApiRoot, "Modules", m, l))).Select(l => $"{m}/{l}"));
        Assert.Empty(missing);
    }

    [Fact]
    public void Modules_only_see_the_contracts_of_other_modules()
    {
        var violations = Modules.SelectMany(module => References(Path.Combine(ApiRoot, "Modules", module))
            .Where(r => r.Reference.StartsWith("BeninConnect.Modules.", StringComparison.Ordinal))
            .Where(r => r.Reference.Split('.')[2] != module && !r.Reference.EndsWith(".Contracts", StringComparison.Ordinal))
            .Select(r => $"{r.File} → {r.Reference}"));
        Assert.Empty(violations.Distinct());
    }

    [Theory]
    [InlineData("Contracts", new[] { "Domain", "Application", "Infrastructure", "Api" })]
    [InlineData("Domain", new[] { "Application", "Infrastructure", "Api" })]
    [InlineData("Application", new[] { "Infrastructure", "Api" })]
    [InlineData("Infrastructure", new[] { "Api" })]
    public void Layers_depend_inwards_only(string layer, string[] forbidden)
    {
        var violations = Modules.SelectMany(module => References(Path.Combine(ApiRoot, "Modules", module, layer))
            .Where(r => forbidden.Any(f => r.Reference == $"BeninConnect.Modules.{module}.{f}" || (f is "Infrastructure" or "Api" && r.Reference == $"BeninConnect.Shared.{f}")))
            .Select(r => $"{r.File} → {r.Reference}"));
        Assert.Empty(violations.Distinct());
    }

    [Fact]
    public void Domain_does_not_depend_on_frameworks()
    {
        var violations = Modules.SelectMany(module => References(Path.Combine(ApiRoot, "Modules", module, "Domain"))
            .Where(r => r.Reference.StartsWith("Microsoft.", StringComparison.Ordinal))
            .Select(r => $"{r.File} → {r.Reference}"));
        Assert.Empty(violations.Distinct());
    }

    [Fact]
    public void Shared_knows_no_module()
    {
        var violations = References(Path.Combine(ApiRoot, "Shared")).Where(r => r.Reference.StartsWith("BeninConnect.Modules.", StringComparison.Ordinal));
        Assert.Empty(violations.Select(r => $"{r.File} → {r.Reference}").Distinct());
    }
}
