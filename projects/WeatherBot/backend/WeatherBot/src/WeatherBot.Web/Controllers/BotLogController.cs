namespace WeatherBot.Web.Controllers;

using Microsoft.AspNetCore.Mvc;
using WeatherBot.Application.Abstractions;

/// <summary>Журнал событий бота — для панели управления.</summary>
[ApiController]
[Route("api/bot/logs")]
public sealed class BotLogController(ILogBuffer logBuffer) : ControllerBase
{
    /// <summary>Сколько записей отдавать, если количество не указано.</summary>
    private const int DefaultCount = 20;

    /// <summary>Верхняя граница запрашиваемого количества записей.</summary>
    private const int MaxCount = 100;

    /// <summary>Возвращает последние записи журнала.</summary>
    /// <param name="count">Сколько записей вернуть (1…100).</param>
    /// <returns>Список строк журнала в хронологическом порядке.</returns>
    [HttpGet]
    public ActionResult<IReadOnlyList<string>> GetLogs([FromQuery] int? count)
    {
        var actualCount = Math.Clamp(count ?? DefaultCount, 1, MaxCount);

        return Ok(logBuffer.GetRecent(actualCount));
    }
}
