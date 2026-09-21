namespace WeatherBot.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WeatherBot.Application.Abstractions;
using WeatherBot.Domain;
using WeatherBot.Infrastructure.BackgroundServices;
using WeatherBot.Infrastructure.Formatting;
using WeatherBot.Infrastructure.Logging;
using WeatherBot.Infrastructure.Messaging;
using WeatherBot.Infrastructure.State;
using WeatherBot.Infrastructure.Weather;

/// <summary>Регистрация инфраструктурного слоя.</summary>
public static class DependencyInjectionExtension
{
    /// <summary>Регистрирует настройки, реализации внешних интеграций и фоновую задачу.</summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Та же коллекция — для цепочки вызовов.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName));
        services.Configure<WeatherSettings>(configuration.GetSection(WeatherSettings.SectionName));
        services.Configure<WeatherBotSettings>(configuration.GetSection(WeatherBotSettings.SectionName));

        services.AddSingleton<IBotStateManager, BotStateManager>();
        services.AddSingleton<ILogBuffer, InMemoryLogBuffer>();
        services.AddSingleton<IWeatherFormatter, WeatherMessageFormatter>();
        services.AddSingleton<ITelegramSender, TelegramSender>();

        services.AddHttpClient<IWeatherProvider, YandexWeatherProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(YandexWeatherProvider.RequestTimeoutSeconds);
        });

        // Провайдер журналирования в память: те же записи, что идут в консоль, видит веб-интерфейс.
        services.AddSingleton<ILoggerProvider>(provider =>
            new InMemoryLoggerProvider(provider.GetRequiredService<ILogBuffer>()));

        services.AddHostedService<WeatherBotBackgroundService>();

        return services;
    }
}
