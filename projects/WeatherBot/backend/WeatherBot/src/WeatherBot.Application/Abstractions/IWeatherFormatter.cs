namespace WeatherBot.Application.Abstractions;

using WeatherBot.Domain;

/// <summary>Формирование текста сообщения о погоде.</summary>
public interface IWeatherFormatter
{
    /// <summary>Формирует текст сообщения для отправки в канал.</summary>
    /// <param name="weather">Данные о погоде.</param>
    /// <returns>Текст в разметке Markdown.</returns>
    string Format(WeatherInfo weather);
}
