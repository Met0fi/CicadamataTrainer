using System.Diagnostics;
using System.Text;

namespace CicadamataTrainer;

internal sealed class Loader
{
    public const string ProcessName = "CICADAMATA";
    private static readonly TimeSpan MarkerTimeout = TimeSpan.FromSeconds(30);

    private readonly GameInstall _install;
    private readonly Action<string> _log;

    public Loader(GameInstall install, Action<string> log)
    {
        _install = install;
        _log = log;
    }

    public static string UserDirectory => TrainerConfig.UserDirectory;

    public static string ScriptFile => Path.Combine(UserDirectory, ModPayload.ScriptFileName);

    public static string LogFile => Path.Combine(UserDirectory, "cicada_trainer.log");

    public static string StatusFile => Path.Combine(UserDirectory, "cicada_trainer.status");

    public string OverrideFile => Path.Combine(_install.Folder, ModPayload.OverrideFileName);

    public static Process? FindGameProcess() => Process.GetProcessesByName(ProcessName).FirstOrDefault();

    public static (int Pid, DateTime Time, string Cheats)? Heartbeat()
    {
        if (!File.Exists(StatusFile))
        {
            return null;
        }
        try
        {
            var values = File.ReadAllLines(StatusFile)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
            if (!values.TryGetValue("pid", out var pidText) || !int.TryParse(pidText, out var pid) || pid <= 0)
            {
                return null;
            }
            if (!values.TryGetValue("time", out var timeText) || !long.TryParse(timeText, out var seconds))
            {
                return null;
            }
            values.TryGetValue("cheats", out var cheats);
            return (pid, DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime, cheats ?? "");
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static bool TrainerAlive()
    {
        var process = FindGameProcess();
        var beat = Heartbeat();
        if (process == null || beat == null)
        {
            return false;
        }
        return beat.Value.Pid == process.Id && DateTime.UtcNow - beat.Value.Time < TimeSpan.FromSeconds(5);
    }

    public bool OverrideInPlace()
    {
        if (!File.Exists(OverrideFile))
        {
            return false;
        }
        var text = File.ReadAllText(OverrideFile);
        return text.Contains(ModPayload.AutoloadName) && text.Contains(ModPayload.ScriptFileName);
    }

    public static long LogLength() => File.Exists(LogFile) ? new FileInfo(LogFile).Length : -1;

    public void WriteScript(string previousMarker)
    {
        Directory.CreateDirectory(UserDirectory);
        File.WriteAllBytes(ScriptFile, ModPayload.Script);
        File.WriteAllBytes(Path.Combine(UserDirectory, ModPayload.FontFileName), ModPayload.Font);
        _log($"script installed ({ModPayload.Script.Length} bytes)");
    }

    public void Arm()
    {
        File.WriteAllText(OverrideFile, ModPayload.OverrideText, new UTF8Encoding(false));
        _log("injection armed");
    }

    public void Disarm()
    {
        if (!File.Exists(OverrideFile))
        {
            return;
        }
        if (!OverrideInPlace())
        {
            _log("left foreign override.cfg in place");
            return;
        }
        File.Delete(OverrideFile);
        _log("injection disarmed, override.cfg removed");
    }

    public void Eject(bool restartGame)
    {
        var wasRunning = FindGameProcess() != null;
        if (wasRunning)
        {
            RestartRunningGame();
        }
        Disarm();
        foreach (var file in new[] { ScriptFile, LogFile, Path.Combine(UserDirectory, ModPayload.FontFileName) })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
                _log($"removed {Path.GetFileName(file)}");
            }
        }
        if (restartGame && wasRunning)
        {
            Launch();
            _log("game restarted without the trainer");
        }
    }

    public bool WaitForMarker(string previousMarker)
    {
        var deadline = DateTime.UtcNow + MarkerTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (FindGameProcess() == null)
            {
                _log("game exited before the trainer reported in");
                return false;
            }
            var marker = LastMarker();
            if (marker != null && marker != previousMarker)
            {
                _log($"trainer active in game: {marker}");
                return true;
            }
            Thread.Sleep(250);
        }
        _log("timed out waiting for the trainer to report in");
        return false;
    }

    public static string? LastMarker()
    {
        if (!File.Exists(LogFile))
        {
            return null;
        }
        try
        {
            using var stream = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string? line = null;
            string? last = null;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("trainer loaded"))
                {
                    last = line.Trim();
                }
            }
            return last;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public Process Launch()
    {
        var info = new ProcessStartInfo(_install.Executable) { WorkingDirectory = _install.Folder, UseShellExecute = true };
        var process = Process.Start(info) ?? throw new InvalidOperationException("The game process could not be started.");
        _log($"game started, pid {process.Id}");
        return process;
    }

    public void RestartRunningGame()
    {
        var running = FindGameProcess();
        if (running == null)
        {
            return;
        }
        _log($"closing the running game (pid {running.Id})");
        running.Kill(true);
        running.WaitForExit(15000);
        for (var i = 0; i < 40 && FindGameProcess() != null; i++)
        {
            Thread.Sleep(250);
        }
        if (FindGameProcess() != null)
        {
            throw new InvalidOperationException("The game did not close in time; trainer files were not removed.");
        }
    }

    public void Load(bool restart)
    {
        var previousMarker = LastMarker();
        WriteScript(previousMarker ?? "");
        Arm();
        try
        {
            if (FindGameProcess() != null)
            {
                if (!restart)
                {
                    throw new InvalidOperationException("The game is already running without the trainer; enable restart or close it first.");
                }
                RestartRunningGame();
            }
            Launch();
            var active = WaitForMarker(previousMarker ?? "");
            _log(active
                ? "trainer loaded into the running game"
                : "the game started but the trainer did not report in; see the log");
        }
        finally
        {
            Disarm();
        }
    }

    public string Describe()
    {
        var running = FindGameProcess();
        var builder = new StringBuilder();
        builder.AppendLine(running == null ? "game: not running" : $"game: running (pid {running.Id})");
        builder.AppendLine(TrainerAlive() ? "trainer: active in game" : OverrideInPlace() ? "trainer: armed, game not confirmed" : "trainer: not loaded");
        builder.AppendLine($"settings: {TrainerConfig.ConfigPath}");
        builder.AppendLine($"last start: {LastMarker() ?? "never"}");
        return builder.ToString().TrimEnd();
    }
}
