namespace WeatherBot.UnitTests;

using AwesomeAssertions;
using WeatherBot.Infrastructure.Common;
using Xunit;

public class SecretMaskerTests
{
    /// <summary>Секрет встречается один раз — заменён на заглушку.</summary>
    [Fact]
    public void Mask_singleOccurrence_replacesWithPlaceholder()
    {
        var text = "Токен: abc123xyz";
        var secret = "abc123xyz";

        var result = SecretMasker.Mask(text, secret);

        result.Should().Be("Токен: ***");
    }

    /// <summary>Секрет встречается несколько раз — заменены все вхождения.</summary>
    [Fact]
    public void Mask_multipleOccurrences_replacesAll()
    {
        var text = "abc123xyz и ещё abc123xyz";
        var secret = "abc123xyz";

        var result = SecretMasker.Mask(text, secret);

        result.Should().Be("*** и ещё ***");
    }

    /// <summary>Пустой секрет — текст без изменений.</summary>
    [Fact]
    public void Mask_emptySecret_returnsTextUnchanged()
    {
        var text = "Токен: abc123xyz";
        var secret = "";

        var result = SecretMasker.Mask(text, secret);

        result.Should().Be("Токен: abc123xyz");
    }

    /// <summary>Null-секрет — текст без изменений.</summary>
    [Fact]
    public void Mask_nullSecret_returnsTextUnchanged()
    {
        var text = "Токен: abc123xyz";

        var result = SecretMasker.Mask(text, null);

        result.Should().Be("Токен: abc123xyz");
    }

    /// <summary>Null-текст — возвращает пустую строку.</summary>
    [Fact]
    public void Mask_nullText_returnsEmptyString()
    {
        var secret = "abc123xyz";

        var result = SecretMasker.Mask(null, secret);

        result.Should().BeEmpty();
    }

    /// <summary>Пустой текст — возвращает пустую строку.</summary>
    [Fact]
    public void Mask_emptyText_returnsEmptyString()
    {
        var secret = "abc123xyz";

        var result = SecretMasker.Mask("", secret);

        result.Should().BeEmpty();
    }

    /// <summary>Секрет отсутствует в тексте — текст без изменений.</summary>
    [Fact]
    public void Mask_secretNotInText_returnsTextUnchanged()
    {
        var text = "Токен: abc123xyz";
        var secret = "def456uvw";

        var result = SecretMasker.Mask(text, secret);

        result.Should().Be("Токен: abc123xyz");
    }

    /// <summary>Секрет — подстрока внутри URL (как токен в Telegram Bot API).</summary>
    [Fact]
    public void Mask_tokenInUrl_replacesToken()
    {
        var text = "Request to https://api.telegram.org/bot123456:ABC-DEF/sendMessage";
        var secret = "123456:ABC-DEF";

        var result = SecretMasker.Mask(text, secret);

        result.Should().Be("Request to https://api.telegram.org/bot***/sendMessage");
    }
}
