using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PhoneDisplay.Server;

internal sealed class Broadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<byte[]>> _clients = new();
    private long _framesPublished;
    private long _framesDropped;

    public int ClientCount => _clients.Count;

    public long FramesPublished => Interlocked.Read(ref _framesPublished);

    public long FramesDropped => Interlocked.Read(ref _framesDropped);

    public ChannelReader<byte[]> Subscribe(out Guid id)
    {
        id = Guid.NewGuid();
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        });

        _clients[id] = channel;
        return channel.Reader;
    }

    public void Unsubscribe(Guid id)
    {
        if (_clients.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public void Publish(byte[] frame)
    {
        Interlocked.Increment(ref _framesPublished);

        foreach (var entry in _clients)
        {
            if (!entry.Value.Writer.TryWrite(frame))
            {
                Interlocked.Increment(ref _framesDropped);
            }
        }
    }

    public void CompleteAll()
    {
        foreach (var entry in _clients)
        {
            entry.Value.Writer.TryComplete();
        }

        _clients.Clear();
    }
}