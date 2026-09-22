namespace WeatherBot.Domain;

/// <summary>Интенсивность осадков.</summary>
public enum PrecipitationStrength
{
    /// <summary>Значение не распознано.</summary>
    Unknown = 0,

    /// <summary>Осадков нет.</summary>
    None,

    /// <summary>Слабые.</summary>
    Weak,

    /// <summary>Умеренные.</summary>
    Moderate,

    /// <summary>Сильные.</summary>
    Heavy,

    /// <summary>Очень сильные.</summary>
    VeryHeavy
}
