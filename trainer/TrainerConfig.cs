namespace CicadamataTrainer;

internal static class TrainerConfig
{
    public static readonly string[] CheatOrder = { "god", "breath", "jumps", "dash", "onehit", "noreload" };

    public const string TeleportAction = "teleport";

    private static readonly Dictionary<string, string> DefaultHotkeys = new()
    {
        ["god"] = "F1",
        ["breath"] = "F2",
        ["jumps"] = "F3",
        ["dash"] = "F4",
        ["onehit"] = "F5",
        ["noreload"] = "F6",
        ["hud"] = "F7",
        ["panic"] = "F8",
        ["allon"] = "F9",
        ["layout"] = "F10",
        ["teleport"] = "F11",
        ["killall"] = "F12",
        ["teleportexit"] = "End",
    };

    private static readonly string[] KeyChoices = Enumerable.Range(1, 12).Select(i => $"F{i}")
        .Concat(Enumerable.Range('A', 26).Select(i => ((char)i).ToString()))
        .Concat(Enumerable.Range(0, 10).Select(i => i.ToString()))
        .Concat(new[] { "Space", "Tab", "Escape", "Insert", "Home", "End", "PageUp", "PageDown", "Delete" })
        .ToArray();

    public static IReadOnlyList<string> KeyOptions => KeyChoices;

    public static IReadOnlyDictionary<string, string> HotkeyDefaults => DefaultHotkeys;

    public static string UserDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CICADAMATA");

    public static string ConfigPath => Path.Combine(UserDirectory, "cicada_trainer.cfg");

    public static string LoadLanguage()
    {
        return "en";
    }

    public static void SaveLanguage(string language)
    {
        var entries = Parse(ReadText());
        entries = Set(entries, "language", "name", "en");
        Write(entries);
    }

    public static void SaveSoundEnabled(bool enabled)
    {
        var cheats = LoadCheats();
        SaveCheats(cheats, enabled);
    }

    public static void UpgradeHotkeys()
    {
        var entries = Parse(ReadText());
        var upgraded = entries.Any(e => e.Section == "hotkeys" && e.Key == "schema" && e.Value.Trim() == "2");
        var hotkeys = LoadHotkeys();
        var duplicate = hotkeys.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != hotkeys.Count;
        var complete = DefaultHotkeys.Keys.All(action => entries.Any(e => e.Section == "hotkeys" && e.Key == action));
        if (upgraded && !duplicate && complete)
        {
            return;
        }
        var legacyDefaults = DefaultHotkeys.All(pair => hotkeys[pair.Key] == (pair.Key == "allon" ? "F10" : pair.Key == "layout" ? "F9" : pair.Value));
        SaveHotkeys(duplicate || legacyDefaults ? DefaultHotkeys : hotkeys);
    }

    public static Dictionary<string, string> AssignHotkey(IReadOnlyDictionary<string, string> current, string action, string key)
    {
        var result = DefaultHotkeys.ToDictionary(pair => pair.Key, pair => current.TryGetValue(pair.Key, out var value) ? value : pair.Value);
        if (!result.TryGetValue(action, out var previous))
        {
            throw new ArgumentException($"Unknown action: {action}", nameof(action));
        }
        key = NormalizeKey(key);
        foreach (var other in result.Keys.ToArray())
        {
            if (other != action && result[other].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                result[other] = previous;
            }
        }
        result[action] = key;
        return result;
    }

    public static Dictionary<string, bool> LoadCheats()
    {
        var result = CheatOrder.ToDictionary(name => name, _ => false);
        foreach (var (section, key, value) in Parse(ReadText()))
        {
            if (section == "cheats" && result.ContainsKey(key))
            {
                result[key] = IsTrue(value);
            }
        }
        return result;
    }

    public static Dictionary<string, string> LoadHotkeys()
    {
        var result = DefaultHotkeys.ToDictionary(kv => kv.Key, kv => kv.Value);
        var saved = new HashSet<string>();
        foreach (var (section, key, value) in Parse(ReadText()))
        {
            if (section == "hotkeys" && result.ContainsKey(key))
            {
                var text = value.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result[key] = NormalizeKey(text);
                    saved.Add(key);
                }
            }
        }
        foreach (var action in new[] { "killall", "teleportexit" })
        {
            if (saved.Contains(action))
            {
                continue;
            }
            var occupied = result.Where(pair => pair.Key != action).Select(pair => pair.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (occupied.Contains(result[action]))
            {
                result[action] = new[] { "F12", "End", "Home", "Insert", "PageUp", "PageDown", "Delete" }.Concat(KeyChoices).First(key => !occupied.Contains(key));
            }
        }
        return result;
    }

    public static int LoadActionCounter(string action)
    {
        foreach (var (section, key, value) in Parse(ReadText()))
        {
            if (section == "actions" && key == action && int.TryParse(value.Trim(), out var parsed))
            {
                return parsed;
            }
        }
        return 0;
    }

    public static bool LoadHudVisible()
    {
        foreach (var (section, key, value) in Parse(ReadText()))
        {
            if (section == "hud" && key == "visible")
            {
                return !IsFalse(value);
            }
        }
        return true;
    }

    public static bool LoadSoundEnabled()
    {
        foreach (var (section, key, value) in Parse(ReadText()))
        {
            if (section == "sound" && key == "enabled")
            {
                return !IsFalse(value);
            }
        }
        return true;
    }

    public static void SaveCheats(IReadOnlyDictionary<string, bool> cheats, bool soundEnabled)
    {
        var entries = Parse(ReadText());
        foreach (var name in CheatOrder)
        {
            entries = Set(entries, "cheats", name, cheats.TryGetValue(name, out var on) && on ? "true" : "false");
        }
        entries = Set(entries, "sound", "enabled", soundEnabled ? "true" : "false");
        Write(entries);
    }

    public static void SaveHotkeys(IReadOnlyDictionary<string, string> hotkeys)
    {
        var entries = Parse(ReadText());
        foreach (var (name, fallback) in DefaultHotkeys)
        {
            var key = hotkeys.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
            entries = Set(entries, "hotkeys", name, $"\"{NormalizeKey(key)}\"");
        }
        entries = Set(entries, "hotkeys", "schema", "2");
        Write(entries);
    }

    public static void BumpTeleportCounter()
    {
        BumpActionCounter(TeleportAction);
    }

    public static void BumpActionCounter(string action)
    {
        if (action is not ("teleport" or "killall" or "teleportexit"))
        {
            throw new ArgumentException($"Unknown action: {action}", nameof(action));
        }
        var entries = Parse(ReadText());
        var next = LoadActionCounter(action) + 1;
        entries = Set(entries, "actions", action, next.ToString());
        Write(entries);
    }

    private static string NormalizeKey(string key) => key switch
    {
        "Page Up" => "PageUp",
        "Page Down" => "PageDown",
        _ => key,
    };

    private static List<Entry> Set(List<Entry> entries, string section, string key, string value)
    {
        var index = entries.FindIndex(e => e.Section == section && e.Key == key);
        if (index >= 0)
        {
            entries[index] = new Entry(section, key, value);
        }
        else
        {
            entries.Add(new Entry(section, key, value));
        }
        return entries;
    }

    private static bool IsTrue(string value) => value.Trim().Trim('"').Equals("true", StringComparison.OrdinalIgnoreCase);

    private static bool IsFalse(string value) => value.Trim().Trim('"').Equals("false", StringComparison.OrdinalIgnoreCase);

    private static string ReadText() => File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : "";

    private static void Write(List<Entry> entries)
    {
        Directory.CreateDirectory(UserDirectory);
        var temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, Serialize(entries));
        File.Move(temp, ConfigPath, true);
    }

    private static List<Entry> Parse(string text)
    {
        var entries = new List<Entry>();
        var section = "";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }
            var split = line.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }
            entries.Add(new Entry(section, line[..split].Trim(), line[(split + 1)..].Trim()));
        }
        return entries;
    }

    private static string Serialize(List<Entry> entries)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var section in entries.Select(e => e.Section).Distinct())
        {
            builder.Append('[').Append(section).Append("]\n\n");
            foreach (var entry in entries.Where(e => e.Section == section))
            {
                builder.Append(entry.Key).Append('=').Append(entry.Value).Append("\n\n");
            }
        }
        return builder.ToString();
    }

    private sealed record Entry(string Section, string Key, string Value);
}
