using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Basira.Platform.Processing;

/// <summary>بث أحداث مباشرة (تقدم المعالجة وتغيّر الأرشيف) لكل المتصفحات المتصلة عبر Server-Sent Events.</summary>
public sealed class EventHub
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public (Guid Id, ChannelReader<string> Reader) Subscribe()
    {
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        var id = Guid.NewGuid();
        _subscribers[id] = ch;
        return (id, ch.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var ch)) ch.Writer.TryComplete();
    }

    public void Publish(string type, object payload)
    {
        var json = JsonSerializer.Serialize(new { type, payload }, Json);
        foreach (var ch in _subscribers.Values) ch.Writer.TryWrite(json);
    }
}
