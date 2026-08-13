using System.Xml.Linq;
using Xunit;

namespace HeroPassport.Architecture.Tests;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void CoreProjectsFollowTheApprovedDependencyDirection()
    {
        var root = FindRepositoryRoot();

        AssertProjectReferences(root, "src/HeroPassport.Domain/HeroPassport.Domain.csproj", []);
        AssertProjectReferences(
            root,
            "src/HeroPassport.Application/HeroPassport.Application.csproj",
            ["../HeroPassport.Domain/HeroPassport.Domain.csproj"]);
        AssertProjectReferences(
            root,
            "src/HeroPassport.Infrastructure/HeroPassport.Infrastructure.csproj",
            ["../HeroPassport.Application/HeroPassport.Application.csproj", "../HeroPassport.Domain/HeroPassport.Domain.csproj"]);
    }

    [Fact]
    public void DomainAndApplicationDoNotReferenceAdapterPackages()
    {
        var root = FindRepositoryRoot();

        Assert.Empty(LoadIncludes(root, "src/HeroPassport.Domain/HeroPassport.Domain.csproj", "PackageReference"));
        Assert.Empty(LoadIncludes(root, "src/HeroPassport.Application/HeroPassport.Application.csproj", "PackageReference"));
    }

    private static void AssertProjectReferences(string root, string projectPath, string[] expected)
    {
        var actual = LoadIncludes(root, projectPath, "ProjectReference");
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    private static string[] LoadIncludes(string root, string projectPath, string elementName)
    {
        return XDocument.Load(Path.Combine(root, projectPath))
            .Descendants(elementName)
            .Select(element => (string?)element.Attribute("Include"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Replace('\\', '/'))
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "HeroPassport.slnx")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find HeroPassport.slnx from the test base directory.");
    }
}
