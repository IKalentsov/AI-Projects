namespace WeatherBot.Application.Abstractions;

using CSharpFunctionalExtensions;
using WeatherBot.Domain;

/// <summary>Получение текущей погоды в точке наблюдения.</summary>
public interface IWeatherProvider
{
    /// <summary>Запрашивает текущую погоду.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Погода либо описание ошибки.</returns>
    Task<Result<WeatherInfo>> GetCurrentAsync(CancellationToken cancellationToken);
}
