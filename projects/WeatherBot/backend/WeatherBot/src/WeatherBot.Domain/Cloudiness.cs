namespace WeatherBot.Domain;

/// <summary>Облачность.</summary>
public enum Cloudiness
{
    /// <summary>Значение не распознано.</summary>
    Unknown = 0,

    /// <summary>Ясно.</summary>
    Clear,

    /// <summary>Малооблачно.</summary>
    PartlyCloudy,

    /// <summary>Облачно.</summary>
    Cloudy,

    /// <summary>Пасмурно.</summary>
    Overcast
}
