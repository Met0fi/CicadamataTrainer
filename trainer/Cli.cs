namespace CicadamataTrainer;

internal static class Cli
{
    private const string Usage = """
        cicadamata-trainer <command> [game folder]

        commands
          status      find the game, show process state, injection state and toggles
          load        load the trainer into the game (starts it, or restarts it)
          eject       remove the trainer files from the game folder and user data
          launch      start the game without the trainer
          toggles     write toggles, e.g. toggles god=on,jumps=on,sound=off
          allon       enable every cheat
          alloff      disable every cheat
          teleport    ask the game to jump to the nearest yellow core
          killall     kill all enemies in the current round
          teleportexit jump to the nearest exit (does not bypass core requirements)
          binds       write key binds, e.g. binds god=F2,teleport=F12

        without a command the graphical trainer is opened
        """;

    public static int Run(string[] args)
    {
        var command = args[0].ToLowerInvariant();
        var positional = args.Skip(1).Where(a => !a.StartsWith("--")).ToArray();
        var takesFolder = command != "toggles";
        var hint = takesFolder && positional.Length > 0 ? positional[0] : null;

        if (command is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            switch (command)
            {
                case "status":
                    Trainer.Status(hint, Console.WriteLine);
                    return 0;
                case "load":
                    Trainer.Load(hint, true, Console.WriteLine);
                    return 0;
                case "eject":
                    Trainer.Eject(hint, Console.WriteLine);
                    return 0;
                case "launch":
                    Trainer.Launch(hint, Console.WriteLine);
                    return 0;
                case "toggles":
                    ApplyToggles(positional);
                    return 0;
                case "allon":
                    Trainer.SaveToggles(TrainerConfig.CheatOrder.ToDictionary(name => name, _ => true), TrainerConfig.LoadSoundEnabled(), Console.WriteLine);
                    return 0;
                case "alloff":
                    Trainer.SaveToggles(TrainerConfig.CheatOrder.ToDictionary(name => name, _ => false), TrainerConfig.LoadSoundEnabled(), Console.WriteLine);
                    return 0;
                case "teleport":
                    Trainer.TeleportToCore(Console.WriteLine);
                    return 0;
                case "killall":
                    Trainer.KillAllEnemies(Console.WriteLine);
                    return 0;
                case "teleportexit":
                    Trainer.TeleportToExit(Console.WriteLine);
                    return 0;
                case "binds":
                    ApplyBinds(positional);
                    return 0;
                default:
                    Console.Error.WriteLine($"unknown command '{command}'");
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"failed: {error.Message}");
            return 1;
        }
    }

    private static void ApplyToggles(string[] pairs)
    {
        var cheats = TrainerConfig.LoadCheats();
        var sound = TrainerConfig.LoadSoundEnabled();
        foreach (var item in pairs.SelectMany(pair => pair.Split(',', StringSplitOptions.RemoveEmptyEntries)))
        {
            var split = item.Split('=', 2);
            var name = split[0].Trim().ToLowerInvariant();
            var on = split.Length == 2 && (split[1].Trim().Equals("on", StringComparison.OrdinalIgnoreCase) || split[1].Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
            if (name == "sound")
            {
                sound = on;
                continue;
            }
            if (!cheats.ContainsKey(name))
            {
                throw new InvalidOperationException($"unknown toggle '{item}', expected god|breath|jumps|dash|onehit|noreload|sound");
            }
            cheats[name] = on;
        }
        Trainer.SaveToggles(cheats, sound, Console.WriteLine);
    }

    private static void ApplyBinds(string[] pairs)
    {
        var hotkeys = TrainerConfig.LoadHotkeys();
        foreach (var item in pairs.SelectMany(pair => pair.Split(',', StringSplitOptions.RemoveEmptyEntries)))
        {
            var split = item.Split('=', 2);
            if (split.Length != 2)
            {
                throw new InvalidOperationException($"expected key=value, got '{item}'");
            }
            var name = split[0].Trim().ToLowerInvariant();
            if (!hotkeys.ContainsKey(name))
            {
                throw new InvalidOperationException($"unknown action '{name}', expected {string.Join('|', TrainerConfig.HotkeyDefaults.Keys)}");
            }
            hotkeys = TrainerConfig.AssignHotkey(hotkeys, name, split[1].Trim());
        }
        Trainer.SaveHotkeys(hotkeys, Console.WriteLine);
    }
}
