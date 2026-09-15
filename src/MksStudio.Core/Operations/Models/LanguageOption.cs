namespace MksStudio.Core.Operations.Models;

public record LanguageOption(string Code, string DisplayName, string NativeName = "")
{
    public string FullDisplay => string.IsNullOrEmpty(NativeName)
        ? $"{DisplayName} ({Code})"
        : $"{DisplayName} - {NativeName} ({Code})";

    public static IReadOnlyList<LanguageOption> SourceLanguages { get; } = new List<LanguageOption>
    {
        new("auto", "Auto Detect (Deteksi Otomatis)", "Automatic"),
        new("en", "English", "English"),
        new("ja", "Japanese", "日本語"),
        new("ko", "Korean", "한국어"),
        new("zh-CN", "Chinese (Simplified)", "简体中文"),
        new("zh-TW", "Chinese (Traditional)", "繁體中文"),
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("es", "Spanish", "Español"),
        new("fr", "French", "Français"),
        new("de", "German", "Deutsch"),
        new("ar", "Arabic", "العربية"),
        new("ru", "Russian", "Русский"),
        new("pt", "Portuguese", "Português"),
        new("it", "Italian", "Italiano"),
        new("nl", "Dutch", "Nederlands"),
        new("pl", "Polish", "Polski"),
        new("tr", "Turkish", "Türkçe"),
        new("th", "Thai", "ไทย"),
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("tl", "Filipino / Tagalog", "Wikang Filipino"),
        new("ms", "Malay", "Bahasa Melayu"),
        new("hi", "Hindi", "हिन्दी")
    };

    public static IReadOnlyList<LanguageOption> TargetLanguages { get; } = new List<LanguageOption>
    {
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("en", "English", "English"),
        new("ja", "Japanese", "日本語"),
        new("ko", "Korean", "한국어"),
        new("zh-CN", "Chinese (Simplified)", "简体中文"),
        new("zh-TW", "Chinese (Traditional)", "繁體中文"),
        new("es", "Spanish", "Español"),
        new("fr", "French", "Français"),
        new("de", "German", "Deutsch"),
        new("ar", "Arabic", "العربية"),
        new("ru", "Russian", "Русский"),
        new("pt", "Portuguese", "Português"),
        new("it", "Italian", "Italiano"),
        new("nl", "Dutch", "Nederlands"),
        new("pl", "Polish", "Polski"),
        new("tr", "Turkish", "Türkçe"),
        new("th", "Thai", "ไทย"),
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("tl", "Filipino / Tagalog", "Wikang Filipino"),
        new("ms", "Malay", "Bahasa Melayu"),
        new("hi", "Hindi", "हिन्दी")
    };
}
