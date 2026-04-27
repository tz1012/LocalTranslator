namespace LocalTranslatorApp.Models;

public sealed class AppSettings
{
    public bool LaunchAtStartup { get; set; }
    public bool FloatingIconEnabled { get; set; } = true;
    public QuickAccessMode QuickAccessMode { get; set; } = QuickAccessMode.FloatingWindow;
    public CloseBehavior CloseBehavior { get; set; } = CloseBehavior.KeepInBackground;
    public string SourceLanguage { get; set; } = "Auto";
    public string TargetLanguage { get; set; } = "Korean";
    public string Theme { get; set; } = "System";
    public string TranslateShortcut { get; set; } = "Ctrl+C,C";
    public string InsertShortcut { get; set; } = "Ctrl+Enter";
    public int DoubleCopyIntervalMs { get; set; } = 600;
    public string LmStudioEndpoint { get; set; } = "http://localhost:1234/v1";
    public string Model { get; set; } = "";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 900;
    public bool CheckForUpdates { get; set; } = true;
    public string UpdateRepository { get; set; } = "tz1012/LocalTranslator";
    public string UpdateAssetPattern { get; set; } = "Setup.exe";
    public string Instruction { get; set; } =
        "Translate naturally for Korean readers while preserving meaning. Keep URLs, code, product names, and proper nouns unchanged.";
}

public enum QuickAccessMode
{
    FloatingWindow,
    MainApp
}

public enum CloseBehavior
{
    KeepInBackground,
    Exit,
    AskEveryTime
}
