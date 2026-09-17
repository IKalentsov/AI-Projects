namespace QwenAnswers.Tests.Architecture;

using System.Xml.Linq;
using FluentAssertions;
using QwenAnswers.Config;
using QwenAnswers.Tests.Support;

/// <summary>
/// Договорённости репозитория: шаблон настроек, защита .env от git, документация и сборка.
/// Эти тесты не требуют заполненного .env и защищают от расхождения кода, документации и конфигов.
/// </summary>
[Trait("Category", "Architecture")]
public class ProjectContractTests
{
    [Fact]
    public void EnvPublicTemplate_HasExactlyRequiredKeys_AllEmpty()
    {
        var assignments = ReadActiveAssignments(TestPaths.EnvPublicFile);

        assignments.Keys.Should().BeEquivalentTo(EnvKeys.Required);
        assignments.Values.Should().OnlyContain(value => value.Length == 0);
    }

    [Fact]
    public void EnvPublicTemplate_DocumentsOptionalKeys()
    {
        var template = File.ReadAllText(TestPaths.EnvPublicFile);

        foreach (var key in EnvKeys.Optional)
            template.Should().Contain(key, $"в шаблоне должен быть закомментированный {key}");
    }

    [Fact]
    public void EnvPublicTemplate_DocumentsLiveTestFlag()
    {
        File.ReadAllText(TestPaths.EnvPublicFile).Should().Contain(LiveTestGate.FlagName);
    }

    [Fact]
    public void GitIgnore_IgnoresRealEnvFile()
    {
        ReadActiveLines(TestPaths.GitIgnoreFile).Should().Contain(".env");
    }

    [Fact]
    public void GitIgnore_DoesNotIgnoreTemplateOrWildcardThatWouldMatchIt()
    {
        var patterns = ReadActiveLines(TestPaths.GitIgnoreFile);

        patterns.Should().NotContain(".env-public");
        patterns.Should().NotContain(".env*", "шаблон .env* исключил бы из git и .env-public");
    }

    [Fact]
    public void Readme_DocumentsEverySetting()
    {
        var readme = File.ReadAllText(TestPaths.ReadmeFile);

        foreach (var key in EnvKeys.All)
            readme.Should().Contain(key, $"README должен описывать {key}");

        readme.Should().Contain(LiveTestGate.FlagName);
        readme.Should().Contain(".env-public");
    }

    [Fact]
    public void AppProject_CopiesEnvFileToOutputDirectory()
    {
        var envItem = FindItem(TestPaths.AppProjectFile, "None", ".env");

        envItem.Should().NotBeNull(
            ".env копируется в выходной каталог — именно оттуда его читает Program.cs");

        // Метаданные MSBuild записываются дочерними элементами, а не атрибутами.
        ((string?)envItem!.Element("CopyToOutputDirectory")).Should().Be("PreserveNewest");
    }

    [Fact]
    public void AppProject_DoesNotCompileTestProjectSources()
    {
        var document = XDocument.Load(TestPaths.AppProjectFile);

        var compileRemovals = document
            .Descendants("Compile")
            .Select(element => element.Attribute("Remove")?.Value)
            .Where(value => value is not null)
            .ToList();

        compileRemovals.Should().Contain(value => value!.Contains("QwenAnswers.Tests"));
    }

    [Fact]
    public void Solution_ListsAppAndTestProjects()
    {
        var paths = XDocument
            .Load(TestPaths.SolutionFile)
            .Descendants("Project")
            .Select(element => (element.Attribute("Path")?.Value ?? string.Empty).Replace('\\', '/'))
            .ToList();

        paths.Should().Contain("QwenAnswers.csproj");
        paths.Should().Contain("QwenAnswers.Tests/QwenAnswers.Tests/QwenAnswers.Tests.csproj");
    }

    [Fact]
    public void TestProject_ReferencesAppProject()
    {
        var references = XDocument
            .Load(TestPaths.TestProjectFile)
            .Descendants("ProjectReference")
            .Select(element => (element.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/'))
            .ToList();

        references.Should().Contain(reference => reference.EndsWith("QwenAnswers.csproj"));
    }

    private static XElement? FindItem(string projectFile, string itemName, string itemValue)
    {
        return XDocument
            .Load(projectFile)
            .Descendants(itemName)
            .FirstOrDefault(element =>
                element.Attribute("Include")?.Value == itemValue
                || element.Attribute("Update")?.Value == itemValue);
    }

    private static IEnumerable<string> ReadActiveLines(string path)
    {
        return File
            .ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));
    }

    private static Dictionary<string, string> ReadActiveAssignments(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in ReadActiveLines(path))
        {
            var separatorIndex = line.IndexOf('=');

            if (separatorIndex <= 0)
                continue;

            result[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
        }

        return result;
    }
}
