using System.Xml.Linq;
using Xunit;

namespace HeroPassport.Architecture.Tests;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void ProjectReferenceGraphMatchesV321Architecture()
    {
        var root = FindRepositoryRoot();

        AssertProjectReferences(root, "src/HeroPassport.Domain/HeroPassport.Domain.csproj", []);
        AssertProjectReferences(root, "src/HeroPassport.Application/HeroPassport.Application.csproj",
            ["../HeroPassport.Domain/HeroPassport.Domain.csproj"]);
        AssertProjectReferences(root, "src/HeroPassport.Infrastructure/HeroPassport.Infrastructure.csproj",
            ["../HeroPassport.Application/HeroPassport.Application.csproj", "../HeroPassport.Domain/HeroPassport.Domain.csproj"]);
        AssertProjectReferences(root, "src/HeroPassport.App/HeroPassport.App.csproj",
            ["../HeroPassport.Application/HeroPassport.Application.csproj", "../HeroPassport.Infrastructure/HeroPassport.Infrastructure.csproj"]);
    }

    [Fact]
    public void DomainAndApplicationDoNotReferenceForbiddenAdapterPackages()
    {
        var root = FindRepositoryRoot();

        AssertPackageReferences(root, "src/HeroPassport.Domain/HeroPassport.Domain.csproj", []);
        AssertPackageReferences(root, "src/HeroPassport.Application/HeroPassport.Application.csproj", []);
    }

    private static void AssertProjectReferences(string root, string relativeProjectPath, string[] expected)
    {
        var actual = LoadIncludes(root, relativeProjectPath, "ProjectReference");
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    private static void AssertPackageReferences(string root, string relativeProjectPath, string[] expected)
    {
        var actual = LoadIncludes(root, relativeProjectPath, "PackageReference");
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    private static string[] LoadIncludes(string root, string relativeProjectPath, string elementName)
    {
        var document = XDocument.Load(Path.Combine(root, relativeProjectPath));
        return document.Descendants(elementName)
            .Select(element => (string?)element.Attribute("Include"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Replace('\\', '/'))
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HeroPassport.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find HeroPassport.slnx from the test base directory.");
    }
}
