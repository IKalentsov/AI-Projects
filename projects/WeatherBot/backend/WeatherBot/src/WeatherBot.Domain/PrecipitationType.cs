namespace WeatherBot.Domain;

/// <summary>Тип осадков.</summary>
public enum PrecipitationType
{
    /// <summary>Значение не распознано.</summary>
    Unknown = 0,

    /// <summary>Осадков нет.</summary>
    None,

    /// <summary>Дождь.</summary>
    Rain,

    /// <summary>Снег.</summary>
    Snow,

    /// <summary>Град.</summary>
    Hail,

    /// <summary>Смешанные осадки.</summary>
    Mixed
}
