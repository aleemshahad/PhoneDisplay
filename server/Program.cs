using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using PhoneDisplay.Server;

var options = ServerOptions.Parse(args);

Interop.SetProcessDPIAware();

var displays = Displays.All();

if (displays.Count == 0)
{
    Console.Error.WriteLine("No attached displays were found.");
    return;
}

Console.WriteLine();
Console.WriteLine("Attached displays:");
foreach (var display in displays)
{
    Console.WriteLine("  " + display);
}

Console.WriteLine();

if (options.ListOnly)
{
    return;
}

var target = Displays.Find(displays, options.Display);

if (target is null)
{
    Console.Error.WriteLine($"Display selector '{options.Display}' did not match any attached display.");
    ServerOptions.PrintUsage();
    return;
}

Capture capture;

try
{
    capture = new Capture(target, options.Quality);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not open display '{target.FriendlyName}': {ex.Message}");
    return;
}

var broadcaster = new Broadcaster();
var shutdown = new CancellationTokenSource();
var startedAt = DateTime.UtcNow;

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var captureLoop = RunCaptureLoopAsync(capture, broadcaster, options.Fps, shutdown.Token);

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}");

var app = builder.Build();

app.MapGet("/", () => Results.Content(ReadAsset("www/index.html"), "text/html; charset=utf-8"));
app.MapGet("/player.css", () => Results.Content(ReadAsset("www/player.css"), "text/css; charset=utf-8"));
app.MapGet("/player.js", () => Results.Content(ReadAsset("www/player.js"), "application/javascript; charset=utf-8"));
app.MapGet("/health", () => Results.Text("ok"));

app.MapGet("/api/displays", () => Results.Json(displays.Select(d => new
{
    d.Index,
    d.DeviceName,
    d.FriendlyName,
    d.X,
    d.Y,
    d.Width,
    d.Height,
    d.IsPrimary,
    Selected = d.Index == target.Index
})));

app.MapGet("/api/status", () => Results.Json(new
{
    display = target.ToString(),
    width = target.Width,
    height = target.Height,
    fps = options.Fps,
    quality = options.Quality,
    clients = broadcaster.ClientCount,
    frames = broadcaster.FramesPublished,
    dropped = broadcaster.FramesDropped,
    uptimeSeconds = (int)(DateTime.UtcNow - startedAt).TotalSeconds
}));

app.Map("/mjpeg", HandleMjpegAsync);
app.Map("/ws", HandleWebSocketAsync);

await app.StartAsync();

Console.WriteLine($"Streaming : {target.FriendlyName}  {target.Width}x{target.Height}  {options.Fps} fps  quality {options.Quality}");
Console.WriteLine();
Console.WriteLine("Open this address on the phone:");
foreach (var url in BuildUrls(options.Port))
{
    Console.WriteLine("  " + url);
}

Console.WriteLine();
Console.WriteLine("WiFi  : phone and PC must be on the same network, then open the http://<PC-ip> address above.");
Console.WriteLine("USB   : turn on USB tethering on the phone and use the 192.168.42.x address above.");
Console.WriteLine("Note  : Windows Firewall must allow this app on the Private network profile.");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await app.WaitForShutdownAsync(shutdown.Token);

broadcaster.CompleteAll();

try
{
    await captureLoop;
}
catch (OperationCanceledException)
{
}

capture.Dispose();

async Task HandleMjpegAsync(HttpContext context)
{
    var token = context.RequestAborted;

    context.Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
    context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    context.Response.Headers.Pragma = "no-cache";

    var reader = broadcaster.Subscribe(out var id);

    try
    {
        await context.Response.Body.FlushAsync(token);

        await foreach (var frame in reader.ReadAllAsync(token))
        {
            var header = Encoding.ASCII.GetBytes(
                "--frame\r\nContent-Type: image/jpeg\r\nContent-Length: " + frame.Length + "\r\n\r\n");

            await context.Response.Body.WriteAsync(header, token);
            await context.Response.Body.WriteAsync(frame, token);
            await context.Response.Body.FlushAsync(token);
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (IOException)
    {
    }
    finally
    {
        broadcaster.Unsubscribe(id);
    }
}

async Task HandleWebSocketAsync(HttpContext context)
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var token = context.RequestAborted;

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var reader = broadcaster.Subscribe(out var id);

    var sender = Task.Run(async () =>
    {
        try
        {
            await foreach (var frame in reader.ReadAllAsync(token))
            {
                if (socket.State != WebSocketState.Open)
                {
                    break;
                }

                await socket.SendAsync(frame, WebSocketMessageType.Binary, true, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }, token);

    var receiver = Task.Run(async () =>
    {
        var buffer = new byte[256];

        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new Memory<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }, token);

    await Task.WhenAny(sender, receiver);

    broadcaster.Unsubscribe(id);

    try
    {
        socket.Abort();
    }
    catch (ObjectDisposedException)
    {
    }
}

static async Task RunCaptureLoopAsync(Capture capture, Broadcaster broadcaster, int fps, CancellationToken token)
{
    var interval = TimeSpan.FromSeconds(1.0 / fps);
    var next = DateTime.UtcNow;

    try
    {
        while (!token.IsCancellationRequested)
        {
            var frame = capture.Grab();

            if (frame is not null)
            {
                broadcaster.Publish(frame);
            }

            next += interval;
            var delay = next - DateTime.UtcNow;

            if (delay <= TimeSpan.Zero)
            {
                next = DateTime.UtcNow;
                delay = interval;
            }

            await Task.Delay(delay, token);
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Capture loop stopped: " + ex.Message);
    }
}

static string ReadAsset(string name)
{
    var assembly = typeof(ServerOptions).Assembly;
    using var stream = assembly.GetManifestResourceStream("PhoneDisplay.Server." + name);

    if (stream is null)
    {
        return string.Empty;
    }

    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}

static IEnumerable<string> BuildUrls(int port)
{
    yield return $"http://127.0.0.1:{port}/";

    string[] addresses;

    try
    {
        addresses = Dns.GetHostAddresses(Dns.GetHostName())
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
            .Select(a => a.ToString())
            .ToArray();
    }
    catch (Exception)
    {
        yield break;
    }

    foreach (var address in addresses)
    {
        yield return $"http://{address}:{port}/";
    }
}