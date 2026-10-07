using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace CicadamataTrainer;

internal static class GameFont
{
    private static readonly PrivateFontCollection EnglishCollection = new();
    private static readonly PrivateFontCollection RussianCollection = new();
    private static readonly IntPtr EnglishData;
    private static readonly IntPtr RussianData;
    private static readonly IntPtr EnglishNative;
    private static readonly IntPtr RussianNative;

    static GameFont()
    {
        (EnglishData, EnglishNative) = Load(ModPayload.Font, EnglishCollection, "Bitpop");
        (RussianData, RussianNative) = Load(ModPayload.RussianFont, RussianCollection, "VCR OSD Mono");
    }

    public static Font Create(float pixels = 14f, string? language = null)
    {
        var russian = (language ?? TrainerConfig.LoadLanguage()).Equals("ru", StringComparison.OrdinalIgnoreCase);
        var family = russian ? RussianCollection.Families[0] : EnglishCollection.Families[0];
        return new Font(family, pixels, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    private static (IntPtr Data, IntPtr Native) Load(byte[] bytes, PrivateFontCollection collection, string description)
    {
        var data = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, data, bytes.Length);
        uint count = 0;
        var native = AddFontMemResourceEx(data, (uint)bytes.Length, IntPtr.Zero, ref count);
        if (native == IntPtr.Zero || count == 0)
        {
            Marshal.FreeHGlobal(data);
            throw new InvalidOperationException($"Could not load {description} font.");
        }
        collection.AddMemoryFont(data, bytes.Length);
        return (data, native);
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr AddFontMemResourceEx(IntPtr font, uint size, IntPtr reserved, ref uint count);
}
