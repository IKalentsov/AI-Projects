namespace WeatherBot.Application;

using Microsoft.Extensions.DependencyInjection;
using WeatherBot.Application.Services;

/// <summary>Регистрация прикладного слоя.</summary>
public static class DependencyInjectionExtension
{
    /// <summary>Регистрирует сервисы прикладного слоя.</summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция — для цепочки вызовов.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Singleton: оркестратор используют и фоновая задача, и Minimal API.
        services.AddSingleton<WeatherDigestService>();

        return services;
    }
}
