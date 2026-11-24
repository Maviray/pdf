namespace Maviray.Pdf;

/// <summary>A font weight, from <see cref="Thin"/> (100) to <see cref="Black"/> (900), as OpenType's <c>usWeightClass</c>.</summary>
public enum FontWeight
{
    /// <summary>100.</summary>
    Thin = 100,

    /// <summary>200.</summary>
    ExtraLight = 200,

    /// <summary>300.</summary>
    Light = 300,

    /// <summary>400, the weight of regular text.</summary>
    Normal = 400,

    /// <summary>500.</summary>
    Medium = 500,

    /// <summary>600.</summary>
    SemiBold = 600,

    /// <summary>700, the weight of bold text.</summary>
    Bold = 700,

    /// <summary>800.</summary>
    ExtraBold = 800,

    /// <summary>900.</summary>
    Black = 900,
}
