namespace WeatherBot.ArchitectureTests;

using System.Reflection;
using Xunit;

/// <summary>
/// Проверяет направление зависимостей между пятью проектами решения через
/// Assembly.GetReferencedAssemblies(). Метод читает **использованные** ссылки из
/// метаданных сборки: компилятор C# выбрасывает объявленную, но неиспользуемую
/// ссылку на проект. Поэтому правила AT-03 и AT-05 проверяют отсутствие
/// **использования** типов, а не объявленные в `.csproj` ссылки.
/// Объявленные и неиспользуемые ссылки (F-10, F-16) — предмет реестра находок,
/// а не архитектурного теста.
///
/// Каждое правило — отдельный тест:
///   AT-01  Domain не использует типы других проектов решения
///   AT-02  Application использует только Domain (не Contracts, Infrastructure, Web)
///   AT-03  Contracts не использует типы Domain (независимо от объявленной ссылки в .csproj)
///   AT-04  Infrastructure не зависит от Web
///   AT-05  Web не использует напрямую типы Domain (видит через Application)
/// </summary>
public class LayerDependenciesTests
{
    private static readonly string[] ProjectAssemblies =
    {
        "WeatherBot.Domain",
        "WeatherBot.Application",
        "WeatherBot.Contracts",
        "WeatherBot.Infrastructure",
        "WeatherBot.Web",
    };

    private static Dictionary<string, AssemblyName[]> GetProjectDependencies()
    {
        var dependencies = new Dictionary<string, AssemblyName[]>(StringComparer.Ordinal);

        foreach (var name in ProjectAssemblies)
        {
            var assembly = Assembly.Load(name);
            dependencies[name] = assembly.GetReferencedAssemblies();
        }

        return dependencies;
    }

    private static void AssertNoDependencyOn(Dictionary<string, AssemblyName[]> deps, string layer, params string[] forbidden)
    {
        var referenced = deps[layer];
        var forbiddenNames = new HashSet<string>(forbidden, StringComparer.Ordinal);

        foreach (var refName in referenced)
        {
            var name = refName.Name;
            if (name is not null && forbiddenNames.Contains(name))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Правило нарушено: {layer} зависит от {name}, но зависимость запрещена.");
            }
        }
    }

    // ─── загружаем зависимости один раз для всех тестов ────────────────

    private static Dictionary<string, AssemblyName[]> Dependencies => LazyDependencies.Value;

    private static readonly Lazy<Dictionary<string, AssemblyName[]>> LazyDependencies =
        new(GetProjectDependencies);

    // ─── канарейка: проверяем, что загрузка сборок и сравнение работают ─

    [Fact]
    public void Canary_authorizedDependency_isVisible()
    {
        // Application обязан видеть Domain, Infrastructure — Application: если сборки
        // не загрузились или GetReferencedAssemblies вернул пустой набор, тест упадёт.
        var deps = GetProjectDependencies();
        Assert.Contains("WeatherBot.Domain", deps["WeatherBot.Application"].Select(n => n.Name), StringComparer.Ordinal);
        Assert.Contains("WeatherBot.Application", deps["WeatherBot.Infrastructure"].Select(n => n.Name), StringComparer.Ordinal);
    }

    // ─── негативная проверка механики: AssertNoDependencyOn бросает при нарушении ─

    [Fact]
    public void AssertNoDependencyOn_throwsWhenForbiddenDependencyPresent()
    {
        var fakeDeps = new Dictionary<string, AssemblyName[]>(StringComparer.Ordinal)
        {
            {
                "FakeLayer",
                new[] { new AssemblyName("WeatherBot.Domain") }
            },
        };

        var ex = Assert.Throws<Xunit.Sdk.XunitException>(() => AssertNoDependencyOn(fakeDeps, "FakeLayer", "WeatherBot.Domain"));

        Assert.True(ex.Message.Contains("FakeLayer зависит от WeatherBot.Domain", StringComparison.Ordinal));
    }

    // ─── правила ────────────────────────────────────────────────────────

    [Fact]
    public void AT01_Domain_usesNoOtherProjectAssembly()
    {
        // Domain — ядро; не использует ни один из четырёх остальных проектов.
        AssertNoDependencyOn(Dependencies, "WeatherBot.Domain",
            "WeatherBot.Application",
            "WeatherBot.Contracts",
            "WeatherBot.Infrastructure",
            "WeatherBot.Web");
    }

    [Fact]
    public void AT02_Application_dependsOnlyOnDomain()
    {
        // Application зависит только от Domain (абстракции + домен).
        AssertNoDependencyOn(Dependencies, "WeatherBot.Application",
            "WeatherBot.Contracts",
            "WeatherBot.Infrastructure",
            "WeatherBot.Web");
    }

    [Fact]
    public void AT03_Contracts_usesNoDomainTypes()
    {
        // Contracts — чистые DTO; не использует типы Domain.
        // Объявленная в .csproj ссылка на Domain выбрасывается компилятором,
        // если ни один тип Domain не используется — тест проверяет именно usage.
        AssertNoDependencyOn(Dependencies, "WeatherBot.Contracts",
            "WeatherBot.Domain",
            "WeatherBot.Application",
            "WeatherBot.Infrastructure",
            "WeatherBot.Web");
    }

    [Fact]
    public void AT04_Infrastructure_doesNotDependOnWeb()
    {
        // Infrastructure реализует абстракции Application; Web выше по уровню.
        AssertNoDependencyOn(Dependencies, "WeatherBot.Infrastructure",
            "WeatherBot.Web");
    }

    [Fact]
    public void AT05_Web_usesNoDomainTypesDirectly()
    {
        // Web общается с доменом через Application; прямое использование типов Domain нарушает Clean Architecture.
        // Объявленная в .csproj ссылка Web → Domain (F-16) также выбрасывается,
        // если ни один тип Domain не используется — тест проверяет именно usage.
        AssertNoDependencyOn(Dependencies, "WeatherBot.Web",
            "WeatherBot.Domain");
    }
}
