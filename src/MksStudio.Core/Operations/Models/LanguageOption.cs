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
        new("en", "English", "English"),
        new("id", "Indonesian", "Bahasa Indonesia"),
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
        new("hi", "Hindi", "हिन्दी"),
        new("uk", "Ukrainian", "Українська"),
        new("sv", "Swedish", "Svenska"),
        new("no", "Norwegian", "Norsk"),
        new("da", "Danish", "Dansk"),
        new("fi", "Finnish", "Suomi"),
        new("el", "Greek", "Ελληνικά"),
        new("he", "Hebrew", "עברית"),
        new("cs", "Czech", "Čeština"),
        new("hu", "Hungarian", "Magyar"),
        new("ro", "Romanian", "Română"),
        new("bg", "Bulgarian", "Български"),
        new("hr", "Croatian", "Hrvatski"),
        new("sr", "Serbian", "Српски"),
        new("sk", "Slovak", "Slovenčina"),
        new("bn", "Bengali", "বাংলা"),
        new("fa", "Persian", "فارسی"),
        new("ur", "Urdu", "اردو"),
        new("ta", "Tamil", "தமிழ்"),
        new("te", "Telugu", "తెలుగు"),
        new("mr", "Marathi", "मराठी"),
        new("gu", "Gujarati", "ગુજરાતી"),
        new("my", "Burmese", "မြန်မာ"),
        new("km", "Khmer", "ខ្មែរ"),
        new("lo", "Lao", "ລາວ"),
        new("sw", "Swahili", "Kiswahili")
    };
}
