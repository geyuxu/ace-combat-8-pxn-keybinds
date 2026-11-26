namespace AceCombat8.Core;

public enum UiLanguage { English, Chinese, Japanese }

public static class Lang
{
    public static UiLanguage Current { get; set; } = UiLanguage.Chinese;
    public static string T(string zh, string en, string ja) => Current switch
    {
        UiLanguage.Chinese => zh,
        UiLanguage.Japanese => ja,
        _ => en
    };
}
