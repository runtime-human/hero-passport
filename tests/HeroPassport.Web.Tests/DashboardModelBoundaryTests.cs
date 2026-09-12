using HeroPassport.Application.Runtime;
using HeroPassport.Web.Services;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class DashboardModelBoundaryTests
{
    [Fact]
    public void DashboardPresentationModelsDoNotExposeApplicationContracts()
    {
        var applicationAssembly = typeof(HeroPassportApplication).Assembly;
        var presentationModels = typeof(HeroPassportDashboardViewModel).Assembly
            .GetTypes()
            .Where(static type =>
                string.Equals(type.Namespace, "HeroPassport.Web.Services", StringComparison.Ordinal) &&
                type.Name.StartsWith("HeroPassportDashboard", StringComparison.Ordinal) &&
                type != typeof(HeroPassportDashboardService))
            .ToArray();

        Assert.NotEmpty(presentationModels);
        foreach (var model in presentationModels)
        {
            foreach (var property in model.GetProperties())
            {
                Assert.False(
                    ReferencesAssembly(property.PropertyType, applicationAssembly),
                    $"{model.Name}.{property.Name} exposes Application contract type '{property.PropertyType}'.");
            }
        }
    }

    private static bool ReferencesAssembly(Type type, System.Reflection.Assembly assembly)
    {
        if (type.Assembly == assembly)
        {
            return true;
        }

        if (type.IsArray)
        {
            return ReferencesAssembly(type.GetElementType()!, assembly);
        }

        return type.IsGenericType &&
            type.GetGenericArguments().Any(argument => ReferencesAssembly(argument, assembly));
    }
}
