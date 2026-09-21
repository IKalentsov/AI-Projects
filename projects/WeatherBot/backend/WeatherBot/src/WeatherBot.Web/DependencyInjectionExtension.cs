namespace WeatherBot.Web;

using WeatherBot.Application;
using WeatherBot.Infrastructure;

/// <summary>Композиция слоёв приложения.</summary>
public static class DependencyInjectionExtension
{
    /// <summary>Регистрирует веб-слой, прикладной слой и инфраструктуру.</summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Та же коллекция — для цепочки вызовов.</returns>
    public static IServiceCollection AddProgramDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddWebDependencies().AddApplication().AddInfrastructure(configuration);

        return services;
    }

    /// <summary>Регистрирует сервисы веб-слоя.</summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция — для цепочки вызовов.</returns>
    public static IServiceCollection AddWebDependencies(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddControllers();

        return services;
    }
}
