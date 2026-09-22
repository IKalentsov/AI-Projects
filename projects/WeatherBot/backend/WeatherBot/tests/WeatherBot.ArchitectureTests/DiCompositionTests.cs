namespace WeatherBot.ArchitectureTests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeatherBot.Application.Abstractions;
using WeatherBot.Infrastructure;
using Xunit;

/// <summary>Проверка композиции DI: все абстракции резолвятся из коллекции сервисов.</summary>
public class DiCompositionTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var collection = new ServiceCollection();

        // Минимальная конфигурация — только ключи, значения не важны для резолва.
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "Telegram:BotToken", "000000:APP_TOKEN" },
            { "OpenMeteo:Latitude", "55.7558" },
            { "OpenMeteo:Longitude", "37.6173" },
            { "OpenMeteo:City", "Москва" },
            { "WeatherBot:IntervalMinutes", "30" },
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        collection.AddInfrastructure(configuration);

        return collection.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(IWeatherProvider))]
    [InlineData(typeof(IWeatherFormatter))]
    [InlineData(typeof(ITelegramSender))]
    [InlineData(typeof(IBotStateManager))]
    [InlineData(typeof(ILogBuffer))]
    [InlineData(typeof(IHttpClientFactory))]
    public void Resolve_allAbstractions_succeeds(Type abstractionType)
    {
        using var provider = BuildServiceProvider();
        var result = provider.GetService(abstractionType);
        Assert.NotNull(result);
    }
}
