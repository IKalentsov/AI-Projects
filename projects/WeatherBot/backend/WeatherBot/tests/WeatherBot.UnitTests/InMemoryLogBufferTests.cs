namespace WeatherBot.UnitTests;

using AwesomeAssertions;
using WeatherBot.Infrastructure.Logging;
using Xunit;

public class InMemoryLogBufferTests
{
    /// <summary>Ёмкость буфера равна 100.</summary>
    [Fact]
    public void Capacity_constant_is100()
    {
        InMemoryLogBuffer.Capacity.Should().Be(100);
    }

    /// <summary>При переполнении вытесняется самая старая запись, количество не превышает Capacity.</summary>
    [Fact]
    public void Add_exceedsCapacity_evictsOldestEntries()
    {
        var buffer = new InMemoryLogBuffer();

        for (var i = 0; i < 150; i++)
        {
            buffer.Add($"Entry-{i}");
        }

        var recent = buffer.GetRecent(200);
        recent.Count.Should().Be(100); // не больше Capacity
    }

    /// <summary>GetRecent(n) возвращает последние n записей в хронологическом порядке.</summary>
    [Fact]
    public void GetRecent_returnsLastNInChronologicalOrder()
    {
        var buffer = new InMemoryLogBuffer();

        for (var i = 1; i <= 50; i++)
        {
            buffer.Add($"Entry-{i}");
        }

        var recent = buffer.GetRecent(5);
        recent.Should().HaveCount(5);
        recent[0].Should().Be("Entry-46");
        recent[1].Should().Be("Entry-47");
        recent[2].Should().Be("Entry-48");
        recent[3].Should().Be("Entry-49");
        recent[4].Should().Be("Entry-50");
    }

    /// <summary>GetRecent(0) возвращает пустой список.</summary>
    [Fact]
    public void GetRecent_zero_returnsEmptyList()
    {
        var buffer = new InMemoryLogBuffer();
        buffer.Add("Entry-1");

        var recent = buffer.GetRecent(0);
        recent.Should().BeEmpty();
    }

    /// <summary>GetRecent(отрицательное) возвращает пустой список.</summary>
    [Fact]
    public void GetRecent_negative_returnsEmptyList()
    {
        var buffer = new InMemoryLogBuffer();
        buffer.Add("Entry-1");

        var recent = buffer.GetRecent(-5);
        recent.Should().BeEmpty();
    }

    /// <summary>Add с пустой строкой отклоняется.</summary>
    [Fact]
    public void Add_emptyString_throwsArgumentException()
    {
        var buffer = new InMemoryLogBuffer();

        Action act = () => buffer.Add("");
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Add с пробельной строкой отклоняется.</summary>
    [Fact]
    public void Add_whitespaceString_throwsArgumentException()
    {
        var buffer = new InMemoryLogBuffer();

        Action act = () => buffer.Add("   ");
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Порядок GetRecent(n) сохраняет хронологию после вытеснения.</summary>
    [Fact]
    public void GetRecent_afterEviction_preservesChronologicalOrder()
    {
        var buffer = new InMemoryLogBuffer();

        for (var i = 1; i <= 120; i++)
        {
            buffer.Add($"Entry-{i}");
        }

        var recent = buffer.GetRecent(10);
        recent.Should().HaveCount(10);
        recent[0].Should().Be("Entry-111");
        recent[9].Should().Be("Entry-120");
    }

    /// <summary>GetRecent(n > Capacity) возвращает все записи.</summary>
    [Fact]
    public void GetRecent_moreThanCapacity_returnsAllEntries()
    {
        var buffer = new InMemoryLogBuffer();

        for (var i = 1; i <= 30; i++)
        {
            buffer.Add($"Entry-{i}");
        }

        var recent = buffer.GetRecent(100);
        recent.Should().HaveCount(30);
    }

    /// <summary>Пустой буфер: GetRecent(n) возвращает пустой список.</summary>
    [Fact]
    public void GetRecent_emptyBuffer_returnsEmptyList()
    {
        var buffer = new InMemoryLogBuffer();

        var recent = buffer.GetRecent(10);
        recent.Should().BeEmpty();
    }
}
