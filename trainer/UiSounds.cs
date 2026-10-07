using System.Runtime.InteropServices;

namespace CicadamataTrainer;

internal static class UiSounds
{
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    private static readonly Dictionary<string, GCHandle> Pinned = new();

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(IntPtr data, IntPtr module, uint flags);

    public static bool Enabled { get; set; } = true;

    public static void Play(string name)
    {
        if (!Enabled)
        {
            return;
        }
        if (!Pinned.TryGetValue(name, out var handle))
        {
            handle = GCHandle.Alloc(ModPayload.Sound(name), GCHandleType.Pinned);
            Pinned[name] = handle;
        }
        PlaySound(handle.AddrOfPinnedObject(), IntPtr.Zero, SndMemory | SndAsync | SndNoDefault);
    }
}
