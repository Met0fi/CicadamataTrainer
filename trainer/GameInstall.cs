using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CicadamataTrainer;

internal sealed class GameInstall
{
    public const string RelativeFolder = @"steamapps\common\CICADAMATA";
    public const string ExecutableName = "CICADAMATA.exe";

    public string Folder = "";
    public string Executable = "";
    public string Pack = "";

    public static GameInstall? Resolve(string? hint)
    {
        foreach (var candidate in Candidates(hint))
        {
            var folder = Normalize(candidate);
            if (folder == null)
            {
                continue;
            }
            var executable = Path.Combine(folder, ExecutableName);
            var pack = Path.ChangeExtension(executable, ".pck");
            if (File.Exists(executable) && File.Exists(pack))
            {
                return new GameInstall { Folder = folder, Executable = executable, Pack = pack };
            }
        }
        return null;
    }

    private static IEnumerable<string> Candidates(string? hint)
    {
        if (!string.IsNullOrWhiteSpace(hint))
        {
            yield return hint!;
        }
        foreach (var library in SteamLibraries())
        {
            yield return Path.Combine(library, RelativeFolder);
        }
        for (var drive = 'C'; drive <= 'H'; drive++)
        {
            yield return $@"{drive}:\Steam\{RelativeFolder}";
            yield return $@"{drive}:\SteamLibrary\{RelativeFolder}";
            yield return $@"{drive}:\Games\Steam\{RelativeFolder}";
        }
    }

    public static IReadOnlyList<string> SteamLibraries()
    {
        var libraries = new List<string>();
        try
        {
            var steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(steamPath))
            {
                libraries.AddRange(ParseLibraryFolders(steamPath));
            }
        }
        catch (Exception)
        {
            return libraries;
        }
        return libraries;
    }

    private static IEnumerable<string> ParseLibraryFolders(string steamPath)
    {
        var libraries = new List<string> { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
        {
            return libraries;
        }
        foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
        {
            libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
        }
        return libraries;
    }

    private static string? Normalize(string candidate)
    {
        var path = candidate.Trim().Trim('"');
        if (path.EndsWith(ExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            path = Path.GetDirectoryName(path) ?? "";
        }
        if (File.Exists(path))
        {
            path = Path.GetDirectoryName(path) ?? "";
        }
        if (!Directory.Exists(path))
        {
            return null;
        }
        return Path.GetFullPath(path);
    }
}
