using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Amanah.Api.Tests.Launch;

/// <summary>
/// SPEC section 10 stays out of v1. Route fragments cover capabilities that would
/// need an API (block user, export, phone change, appeals, payments, drafts, push,
/// matching, reopen). The web shell check covers PWA, English UI, and link-preview tags.
/// Published-report edits and irrevocable resolution stay in the lifecycle tests.
/// </summary>
public class OutOfScopeGuardTests
{
    private static readonly string[] ForbiddenRouteFragments =
    [
        "/block",
        "/export",
        "/preferences",
        "/payment",
        "/escrow",
        "/draft",
        "/appeal",
        "/push",
        "/match",
        "/reopen",
        "/display-name",
        "/phone",
        "/geocode",
        "/visibility",
        "/profanity",
    ];

    [Fact]
    public void Api_routes_omit_v1_out_of_scope_capabilities()
    {
        var routes = ApiRouteCatalog.Templates();
        Assert.NotEmpty(routes);

        foreach (var fragment in ForbiddenRouteFragments)
        {
            Assert.DoesNotContain(
                routes,
                route => route.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Web_shell_is_an_arabic_browser_app()
    {
        var webRoot = RepoPaths.WebRoot();

        Assert.False(File.Exists(Path.Combine(webRoot, "ngsw-config.json")));
        Assert.False(File.Exists(Path.Combine(webRoot, "src", "manifest.webmanifest")));

        var angularJson = File.ReadAllText(Path.Combine(webRoot, "angular.json"));
        Assert.DoesNotContain("serviceWorker", angularJson, StringComparison.OrdinalIgnoreCase);

        var index = File.ReadAllText(Path.Combine(webRoot, "src", "index.html"));
        Assert.Contains("lang=\"ar\"", index, StringComparison.Ordinal);
        Assert.DoesNotContain("rel=\"manifest\"", index, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("property=\"og:", index, StringComparison.OrdinalIgnoreCase);

        var locales = Directory.GetDirectories(Path.Combine(webRoot, "src", "assets", "i18n"));
        var locale = Assert.Single(locales);
        var localeName = Path.GetFileName(locale);
        Assert.Equal("ar", localeName ?? string.Empty);
    }
}

internal static class ApiRouteCatalog
{
    public static IReadOnlyList<string> Templates()
    {
        var assembly = typeof(ApiAssemblyMarker).Assembly;
        var routes = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !typeof(ControllerBase).IsAssignableFrom(type))
            {
                continue;
            }

            var controllerRoute = type.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var http in method.GetCustomAttributes().OfType<HttpMethodAttribute>())
                {
                    routes.Add(Combine(controllerRoute, http.Template));
                }
            }
        }

        return routes;
    }

    private static string Combine(string controllerRoute, string? actionTemplate)
    {
        if (string.IsNullOrEmpty(actionTemplate))
        {
            return controllerRoute;
        }

        return $"{controllerRoute.TrimEnd('/')}/{actionTemplate.TrimStart('/')}";
    }
}

internal static class RepoPaths
{
    public static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "web", "angular.json");
            if (File.Exists(candidate))
            {
                return Path.Combine(dir.FullName, "web");
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("web/angular.json was not found above the test output directory.");
    }
}
