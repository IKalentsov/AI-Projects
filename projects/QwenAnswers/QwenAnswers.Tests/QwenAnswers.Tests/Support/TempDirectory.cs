namespace QwenAnswers.Tests.Support;

/// <summary>
/// Временный каталог для тестов, работающих с файлами. Удаляется при <see cref="Dispose"/>.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "QwenAnswers.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Полный путь к созданному каталогу.</summary>
    public string Root { get; }

    /// <summary>Путь к файлу внутри каталога (файл при этом не создаётся).</summary>
    public string GetPath(string fileName) => Path.Combine(Root, fileName);

    /// <summary>Создаёт файл с указанным содержимым и возвращает его путь.</summary>
    public string WriteFile(string fileName, IEnumerable<string> lines)
    {
        var path = GetPath(fileName);
        File.WriteAllLines(path, lines);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Каталог уже удалён или занят — для теста это не важно.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
