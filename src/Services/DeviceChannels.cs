using System.Collections.Concurrent;
using System.Threading.Channels;

namespace XamantaSDK.Services;

public sealed class DeviceChannels<T>
{
    private readonly ConcurrentDictionary<string, Channel<T>> _channels = new();

    public Channel<T> Register(string deviceId)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true });
        _channels[deviceId] = channel;
        return channel;
    }

    public void Unregister(string deviceId, Channel<T> channel)
    {
        _channels.TryRemove(new KeyValuePair<string, Channel<T>>(deviceId, channel));
        channel.Writer.TryComplete();
    }

    public void Remove(string deviceId)
    {
        if (_channels.TryRemove(deviceId, out var channel))
            channel.Writer.TryComplete();
    }

    public bool TryGetWriter(string deviceId, out ChannelWriter<T> writer)
    {
        if (_channels.TryGetValue(deviceId, out var channel))
        {
            writer = channel.Writer;
            return true;
        }

        writer = null!;
        return false;
    }

    public bool IsConnected(string deviceId) => _channels.ContainsKey(deviceId);
}
