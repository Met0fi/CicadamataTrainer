namespace CicadamataTrainer;

internal static class Localizer
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["title"] = "CICADAMATA TRAINER", ["website"] = "WEBSITE", ["folder"] = "Game folder", ["browse"] = "Browse", ["load"] = "Load trainer", ["eject"] = "Eject trainer",
        ["closed"] = "Game closed", ["active"] = "Trainer active", ["running"] = "Game running — trainer not loaded", ["missing"] = "Game not found — choose its folder.", ["working"] = "Working...",
        ["cheats"] = "Cheats", ["god"] = "God mode — no damage", ["breath"] = "Underwater safety", ["jumps"] = "Infinite jumps", ["dash"] = "Infinite dash", ["onehit"] = "One-bullet kill", ["noreload"] = "No weapon recharge",
        ["disable"] = "Disable all", ["enable"] = "Enable all", ["core"] = "Teleport to core", ["kill"] = "Kill all enemies", ["exit"] = "Teleport to exit", ["settings"] = "Settings", ["details"] = "Details", ["hide_details"] = "Hide details", ["hint"] = "Set keys in Settings. F10 edits the HUD; Esc finishes.",
        ["sound"] = "Sounds", ["binds"] = "Binds", ["reset"] = "Reset binds", ["close"] = "Close", ["settings_title"] = "TRAINER SETTINGS", ["function"] = "Function", ["key"] = "Key", ["click"] = "CLICK ON ME", ["hud"] = "Hide / show HUD", ["panic"] = "Disable all", ["allon"] = "Enable all", ["layout"] = "Edit / lock HUD", ["teleport"] = "Teleport to core", ["killall"] = "Kill all enemies", ["teleportexit"] = "Teleport to exit",
        ["press"] = "Press any key", ["bind"] = "Bind", ["cancel"] = "Escape to cancel", ["failed"] = "Could not save this bind.", ["unsupported"] = "This key is not supported.", ["operation_failed"] = "Operation failed — open Details", ["diagnostics"] = "Diagnostics", ["open_log"] = "Open log",
    };

    public static bool IsRussian => false;
    public static string Current => "en";
    public static string Get(string key) => English.TryGetValue(key, out var value) ? value : key;
}
