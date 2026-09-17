namespace QwenAnswers.Tests.Support;

/// <summary>
/// Пути к файлам репозитория.
/// Тесты проверяют реальные .env / .env-public / README / .csproj, поэтому корень
/// ищется подъёмом от каталога сборки до файла решения — путь не зависит от глубины bin.
/// </summary>
public static class TestPaths
{
    private const string SolutionFileName = "QwenAnswers.slnx";

    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>Реальный файл настроек пользователя (не коммитится в git).</summary>
    public static string EnvFile => Path.Combine(RepoRoot, ".env");

    /// <summary>Шаблон настроек из репозитория.</summary>
    public static string EnvPublicFile => Path.Combine(RepoRoot, ".env-public");

    public static string GitIgnoreFile => Path.Combine(RepoRoot, ".gitignore");

    public static string ReadmeFile => Path.Combine(RepoRoot, "README.md");

    public static string AppProjectFile => Path.Combine(RepoRoot, "QwenAnswers.csproj");

    public static string SolutionFile => Path.Combine(RepoRoot, SolutionFileName);

    public static string TestProjectFile => Path.Combine(
        RepoRoot, "QwenAnswers.Tests", "QwenAnswers.Tests", "QwenAnswers.Tests.csproj");

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Не удалось найти '{SolutionFileName}' выше каталога '{AppContext.BaseDirectory}'.");
    }
}
