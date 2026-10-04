namespace PhoneDisplay.Server;

internal sealed class ServerOptions
{
    public int Port { get; private init; } = 8090;
    public string? Display { get; private init; }
    public int Fps { get; private init; } = 30;
    public long Quality { get; private init; } = 80;
    public bool ListOnly { get; private init; }

    public static ServerOptions Parse(string[] args)
    {
        var port = 8090;
        var fps = 30;
        var quality = 80;
        string? display = null;
        var listOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i].ToLowerInvariant();

            switch (key)
            {
                case "--list":
                case "-l":
                    listOnly = true;
                    break;

                case "--port":
                case "-p":
                    if (++i < args.Length && int.TryParse(args[i], out var p))
                    {
                        port = p;
                    }
                    break;

                case "--display":
                case "-d":
                    if (++i < args.Length)
                    {
                        display = args[i];
                    }
                    break;

                case "--fps":
                case "-f":
                    if (++i < args.Length && int.TryParse(args[i], out var f))
                    {
                        fps = f;
                    }
                    break;

                case "--quality":
                case "-q":
                    if (++i < args.Length && int.TryParse(args[i], out var q))
                    {
                        quality = q;
                    }
                    break;

                case "--help":
                case "-h":
                case "/?":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }
        }

        return new ServerOptions
        {
            Port = Math.Clamp(port, 1024, 65535),
            Display = display,
            Fps = Math.Clamp(fps, 1, 60),
            Quality = Math.Clamp(quality, 25, 95),
            ListOnly = listOnly
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("PhoneDisplay Server");
        Console.WriteLine();
        Console.WriteLine("  PhoneDisplay.Server.exe [options]");
        Console.WriteLine();
        Console.WriteLine("  -l, --list              List attached displays and exit");
        Console.WriteLine("  -d, --display <sel>     Display index or name fragment (default: first)");
        Console.WriteLine("  -p, --port <n>          Listen port (default: 8090)");
        Console.WriteLine("  -f, --fps <n>           Capture frame rate 1-60 (default: 30)");
        Console.WriteLine("  -q, --quality <n>       JPEG quality 25-95 (default: 80)");
        Console.WriteLine("  -h, --help              Show this help");
    }
}