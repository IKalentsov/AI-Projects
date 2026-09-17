namespace QwenAnswers.ConsoleAbstraction;

/// <summary>
/// Результат нажатия клавиши пользователем.
/// </summary>
public record ConsoleKeyInfoResult(ConsoleKey Key, char CharValue, bool CtrlPressed)
{
    /// <summary>
    /// Создаёт результат из <see cref="ConsoleKeyInfo"/>, вычисляя признак нажатого Ctrl.
    /// </summary>
    public static ConsoleKeyInfoResult FromConsoleKeyInfo(ConsoleKeyInfo keyInfo)
    {
        return new ConsoleKeyInfoResult(
            keyInfo.Key,
            keyInfo.KeyChar,
            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control));
    }
}

/// <summary>
/// Абстракция над консолью для тестируемости.
/// </summary>
public interface IConsole
{
    /// <summary>
    /// Выводит строку без перевода строки.
    /// </summary>
    void Write(string value);

    /// <summary>
    /// Выводит строку с переводом строки.
    /// </summary>
    void WriteLine(string value);

    /// <summary>
    /// Читает строку ввода от пользователя. null — ввод закончился (EOF).
    /// </summary>
    string? ReadLine();

    /// <summary>
    /// Проверяет, есть ли доступные клавиши в буфере ввода.
    /// При перенаправленном вводе может бросить <see cref="InvalidOperationException"/>.
    /// </summary>
    bool KeyAvailable { get; }

    /// <summary>
    /// Читает нажатую клавишу (блокирует до нажатия).
    /// </summary>
    ConsoleKeyInfoResult ReadKey(bool intercept);
}
