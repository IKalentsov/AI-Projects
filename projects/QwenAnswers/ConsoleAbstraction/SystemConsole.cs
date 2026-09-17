namespace QwenAnswers.ConsoleAbstraction;

/// <summary>
/// Реализация IConsole, обернутая вокруг System.Console.
/// </summary>
public class SystemConsole : IConsole
{
    public void Write(string value) => Console.Write(value);

    public void WriteLine(string value) => Console.WriteLine(value);

    public string? ReadLine() => Console.ReadLine();

    public bool KeyAvailable => Console.KeyAvailable;

    public ConsoleKeyInfoResult ReadKey(bool intercept)
    {
        return ConsoleKeyInfoResult.FromConsoleKeyInfo(Console.ReadKey(intercept));
    }
}
