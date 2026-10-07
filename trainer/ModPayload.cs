using System.Reflection;
using System.Text;

namespace CicadamataTrainer;

internal static class ModPayload
{
    public const string Version = "2.0.0";
    public const string ScriptFileName = "cicada_trainer.gd";
    public const string FontFileName = "cicada_trainer_font.otf";
    public const string OverrideFileName = "override.cfg";
    public const string AutoloadName = "CicadaTrainer";
    public const string AutoloadPath = "*user://" + ScriptFileName;

    public static string OverrideText => $"[autoload]\n\n{AutoloadName}=\"{AutoloadPath}\"\n";

    public static byte[] Script => Read("cicada_trainer.gd");

    public static byte[] Banner => Read("banner.png");

    public static byte[] Font => Read("bitpop.otf");

    public static byte[] RussianFont => Read("vcr_osd_mono.ttf");

    public static byte[] Sound(string name) => Read($"sound_{name}.wav");

    public static string ScriptText() => Encoding.UTF8.GetString(Script);

    private static byte[] Read(string logicalName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource {logicalName} is missing from the trainer build.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
