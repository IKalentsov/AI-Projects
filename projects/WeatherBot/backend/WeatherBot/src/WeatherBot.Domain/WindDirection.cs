namespace WeatherBot.Domain;

/// <summary>Направление ветра.</summary>
public enum WindDirection
{
    /// <summary>Значение не распознано (в том числе штиль).</summary>
    Unknown = 0,

    /// <summary>Север.</summary>
    North,

    /// <summary>Северо-восток.</summary>
    NorthEast,

    /// <summary>Восток.</summary>
    East,

    /// <summary>Юго-восток.</summary>
    SouthEast,

    /// <summary>Юг.</summary>
    South,

    /// <summary>Юго-запад.</summary>
    SouthWest,

    /// <summary>Запад.</summary>
    West,

    /// <summary>Северо-запад.</summary>
    NorthWest
}
