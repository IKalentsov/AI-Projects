namespace QwenAnswers.Tests.Unit;

using FluentAssertions;
using QwenAnswers.ConsoleAbstraction;

/// <summary>
/// Преобразование клавиши консоли в результат, которым пользуется чат (в частности — детект Ctrl).
/// </summary>
public class ConsoleKeyInfoResultTests
{
    private static ConsoleKeyInfo Pressed(char character, ConsoleKey key, bool shift = false, bool alt = false, bool control = false)
        => new(character, key, shift, alt, control);

    [Fact]
    public void FromConsoleKeyInfo_RegularKeyWithoutModifiers_IsNotCtrl()
    {
        var result = ConsoleKeyInfoResult.FromConsoleKeyInfo(Pressed('a', ConsoleKey.A));

        result.Key.Should().Be(ConsoleKey.A);
        result.CharValue.Should().Be('a');
        result.CtrlPressed.Should().BeFalse();
    }

    [Fact]
    public void FromConsoleKeyInfo_CtrlPressed_IsDetected()
    {
        var result = ConsoleKeyInfoResult.FromConsoleKeyInfo(Pressed('c', ConsoleKey.C, control: true));

        result.CtrlPressed.Should().BeTrue();
    }

    [Fact]
    public void FromConsoleKeyInfo_CtrlWithShift_IsStillDetected()
    {
        var result = ConsoleKeyInfoResult.FromConsoleKeyInfo(Pressed('c', ConsoleKey.C, shift: true, control: true));

        result.CtrlPressed.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FromConsoleKeyInfo_OtherModifiers_AreNotTreatedAsCtrl(bool shift, bool alt)
    {
        var result = ConsoleKeyInfoResult.FromConsoleKeyInfo(Pressed('a', ConsoleKey.A, shift, alt));

        result.CtrlPressed.Should().BeFalse();
    }

    [Fact]
    public void FromConsoleKeyInfo_Escape_KeepsKeyAndHasNoCharacter()
    {
        var result = ConsoleKeyInfoResult.FromConsoleKeyInfo(Pressed('\0', ConsoleKey.Escape));

        result.Key.Should().Be(ConsoleKey.Escape);
        result.CharValue.Should().Be('\0');
        result.CtrlPressed.Should().BeFalse();
    }

    [Fact]
    public void FromConsoleKeyInfo_Enter_KeepsCarriageReturnCharacter()
    {
        var result = ConsoleKeyInfoResult.FromConsoleKeyInfo(Pressed('\r', ConsoleKey.Enter));

        result.Key.Should().Be(ConsoleKey.Enter);
        result.CharValue.Should().Be('\r');
    }
}
