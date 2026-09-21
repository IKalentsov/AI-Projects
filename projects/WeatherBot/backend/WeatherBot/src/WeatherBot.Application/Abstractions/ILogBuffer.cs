namespace WeatherBot.Application.Abstractions;

/// <summary>Кольцевой буфер последних записей журнала — для показа на странице статуса.</summary>
public interface ILogBuffer
{
    /// <summary>Добавляет запись. Старые записи вытесняются при переполнении.</summary>
    /// <param name="message">Готовая строка записи.</param>
    void Add(string message);

    /// <summary>Возвращает последние записи в хронологическом порядке.</summary>
    /// <param name="count">Сколько записей вернуть.</param>
    /// <returns>Не более <paramref name="count"/> последних записей.</returns>
    IReadOnlyList<string> GetRecent(int count);
}
