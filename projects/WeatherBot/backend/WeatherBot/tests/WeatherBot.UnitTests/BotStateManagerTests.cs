namespace WeatherBot.UnitTests;

using AwesomeAssertions;
using WeatherBot.Infrastructure.State;
using Xunit;

public class BotStateManagerTests
{
    /// <summary>Стартовое состояние: не запущен, LastSentAt и NextTickAt пусты.</summary>
    [Fact]
    public void Ctor_initialState_notRunningAndEmptyTimestamps()
    {
        var state = new BotStateManager();

        state.IsRunning.Should().BeFalse();
        state.LastSentAt.Should().BeNull();
        state.NextTickAt.Should().BeNull();
    }

    /// <summary>Start() устанавливает IsRunning в true.</summary>
    [Fact]
    public void Start_setsIsRunningToTrue()
    {
        var state = new BotStateManager();

        state.Start();

        state.IsRunning.Should().BeTrue();
    }

    /// <summary>Stop() устанавливает IsRunning в false и обнуляет NextTickAt.</summary>
    [Fact]
    public void Stop_setsIsRunningToFalseAndClearsNextTickAt()
    {
        var state = new BotStateManager();
        var now = DateTimeOffset.UtcNow;

        state.Start();
        state.MarkNextTick(now);
        state.Stop();

        state.IsRunning.Should().BeFalse();
        state.NextTickAt.Should().BeNull();
    }

    /// <summary>Stop() не влияет на LastSentAt.</summary>
    [Fact]
    public void Stop_doesNotAffectLastSentAt()
    {
        var state = new BotStateManager();
        var sentAt = DateTimeOffset.UtcNow;

        state.MarkSent(sentAt);
        state.Stop();

        state.LastSentAt.Should().Be(sentAt);
    }

    /// <summary>MarkSent() фиксирует момент отправки.</summary>
    [Fact]
    public void MarkSent_recordsTimestamp()
    {
        var state = new BotStateManager();
        var sentAt = DateTimeOffset.UtcNow;

        state.MarkSent(sentAt);

        state.LastSentAt.Should().Be(sentAt);
    }

    /// <summary>MarkNextTick() устанавливает момент следующей отправки.</summary>
    [Fact]
    public void MarkNextTick_setsNextTickAt()
    {
        var state = new BotStateManager();
        var tickAt = DateTimeOffset.UtcNow.AddMinutes(30);

        state.MarkNextTick(tickAt);

        state.NextTickAt.Should().Be(tickAt);
    }

    /// <summary>MarkNextTick(null) обнуляет NextTickAt.</summary>
    [Fact]
    public void MarkNextTick_null_clearsNextTickAt()
    {
        var state = new BotStateManager();
        var tickAt = DateTimeOffset.UtcNow.AddMinutes(30);

        state.MarkNextTick(tickAt);
        state.MarkNextTick(null);

        state.NextTickAt.Should().BeNull();
    }

    /// <summary>Параллельные вызовы Start/Stop из множества потоков не бросают исключений.</summary>
    [Fact]
    public void Concurrent_StartStop_noExceptions()
    {
        var state = new BotStateManager();
        var exceptions = new List<Exception>();
        const int iterations = 1000;

        Parallel.For(0, iterations, @int =>
        {
            try
            {
                _ = state.IsRunning.GetHashCode();
                _ = state.LastSentAt?.GetHashCode() ?? 0;
                _ = state.NextTickAt?.GetHashCode() ?? 0;
                state.Start();
                state.Stop();
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        });

        exceptions.Should().BeEmpty();
    }

    /// <summary>Параллельные MarkSent/MarkNextTick не бросают исключений; после гонки состояние стабильно.</summary>
    [Fact]
    public void Concurrent_MarkSentAndMarkNextTick_noExceptionsAndStableState()
    {
        var state = new BotStateManager();
        var exceptions = new List<Exception>();

        Parallel.Invoke(
            () =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    try
                    {
                        state.MarkSent(DateTimeOffset.UtcNow.AddMilliseconds(i));
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }
            },
            () =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    try
                    {
                        state.MarkNextTick(i % 2 == 0 ? DateTimeOffset.UtcNow.AddMinutes(i) : null);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }
            });

        exceptions.Should().BeEmpty();

        // После гонки состояние должно быть стабильным: LastSentAt установлен, NextTickAt — либо null, либо значение.
        state.LastSentAt.Should().NotBeNull();
    }
}
