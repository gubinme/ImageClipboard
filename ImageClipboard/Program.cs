namespace ImageClipboard;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--generate-icon")
        {
            IconFactory.SaveApplicationIcon(args[1]);
            return;
        }

        using var mutex = new Mutex(true, "ImageClipboard_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
