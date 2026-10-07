using System.Diagnostics;

namespace CicadamataTrainer;

internal static class Trainer
{
    public static GameInstall RequireInstall(string? hint, Action<string> log)
    {
        var install = GameInstall.Resolve(hint);
        if (install == null)
        {
            throw new InvalidOperationException("CICADAMATA was not found; pass the game folder as an argument.");
        }
        log($"game folder: {install.Folder}");
        return install;
    }

    public static Loader RequireLoader(string? hint, Action<string> log) => new(RequireInstall(hint, log), log);

    public static void Load(string? hint, bool restart, Action<string> log)
    {
        var loader = RequireLoader(hint, log);
        TrainerConfig.UpgradeHotkeys();
        TrainerConfig.SaveCheats(TrainerConfig.LoadCheats(), TrainerConfig.LoadSoundEnabled());
        loader.Load(restart);
    }

    public static void Eject(string? hint, Action<string> log)
    {
        RequireLoader(hint, log).Eject(true);
        log("trainer removed; the game was restarted without the trainer");
    }

    public static void Status(string? hint, Action<string> log)
    {
        var install = RequireInstall(hint, log);
        foreach (var line in new Loader(install, log).Describe().Split('\n'))
        {
            log(line);
        }
        log("cheats: " + string.Join(", ", TrainerConfig.LoadCheats().Where(kv => kv.Value).Select(kv => kv.Key).DefaultIfEmpty("none")));
        log($"sounds: {(TrainerConfig.LoadSoundEnabled() ? "on" : "off")}");
    }

    public static void Launch(string? hint, Action<string> log) => RequireLoader(hint, log).Launch();

    public static void SaveToggles(IReadOnlyDictionary<string, bool> cheats, bool soundEnabled, Action<string> log)
    {
        TrainerConfig.SaveCheats(cheats, soundEnabled);
        UiSounds.Enabled = soundEnabled;
        log("cheats: " + string.Join(", ", cheats.Where(kv => kv.Value).Select(kv => kv.Key).DefaultIfEmpty("none")));
        log($"sounds: {(soundEnabled ? "on" : "off")}");
    }

    public static void SaveHotkeys(IReadOnlyDictionary<string, string> hotkeys, Action<string> log)
    {
        TrainerConfig.SaveHotkeys(hotkeys);
        log("binds: " + string.Join(", ", hotkeys.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public static void ResetHotkeys(Action<string> log)
    {
        TrainerConfig.SaveHotkeys(TrainerConfig.HotkeyDefaults.ToDictionary(kv => kv.Key, kv => kv.Value));
        log("binds reset to defaults");
    }

    public static void TeleportToCore(Action<string> log)
    {
        TrainerConfig.BumpTeleportCounter();
        log(Loader.TrainerAlive() ? "teleport requested, the game will jump to the nearest core" : "teleport requested, but the trainer is not running in the game");
    }

    public static bool TrainerActiveInRunningGame() => Loader.TrainerAlive();

    public static void KillAllEnemies(Action<string> log)
    {
        TrainerConfig.BumpActionCounter("killall");
        log(Loader.TrainerAlive() ? "kill all enemies requested for the current round" : "kill all enemies requested, but the trainer is not running in the game");
    }

    public static void TeleportToExit(Action<string> log)
    {
        TrainerConfig.BumpActionCounter("teleportexit");
        log(Loader.TrainerAlive() ? "teleport requested to the nearest exit; the game's core requirement still applies" : "teleport to exit requested, but the trainer is not running in the game");
    }

    public static void OpenLog(Action<string> log)
    {
        var file = Loader.LogFile;
        if (!File.Exists(file))
        {
            log($"no trainer log yet at {file}");
            return;
        }
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }
}
