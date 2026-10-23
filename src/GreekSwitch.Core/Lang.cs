namespace GreekSwitch.Core;

/// <summary>The two keyboard languages GreekSwitch arbitrates between.</summary>
public enum Lang
{
    Greek,
    English,
}

public static class LangExtensions
{
    public static Lang Other(this Lang lang) => lang == Lang.Greek ? Lang.English : Lang.Greek;

    /// <summary>Short code used in log events ("GR"/"EN").</summary>
    public static string Code(this Lang lang) => lang == Lang.Greek ? "GR" : "EN";
}
