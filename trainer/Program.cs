namespace CicadamataTrainer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            ParentConsole.Attach();
            return Cli.Run(args);
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrainerForm());
        return 0;
    }
}
