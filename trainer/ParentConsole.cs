using System.Runtime.InteropServices;

namespace CicadamataTrainer;

internal static class ParentConsole
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    public static void Attach()
    {
        if (IsConsoleHandle(GetStdHandle(StdOutputHandle)))
        {
            AttachConsole(AttachParentProcess);
        }
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    private static bool IsConsoleHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1) && GetConsoleMode(handle, out _);
}
