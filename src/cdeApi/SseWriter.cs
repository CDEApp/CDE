using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace cdeApi;

/// <summary>
/// Server-Sent Events writer. Each event's JSON is serialized with a reused <see cref="Utf8JsonWriter"/> and the
/// source-generated type info into a reused scratch buffer, then the complete frame is copied into the response's
/// <see cref="PipeWriter"/>: no per-row JSON or <c>data:</c> strings, and a serialization failure leaves no partial
/// frame in the pipe. Wire format per event is <c>event: NAME\ndata: {single-line json}\n\n</c>.
/// Not thread-safe: all writes must come from the single drain loop (<see cref="DrainAsync{T}"/>).
/// </summary>
public sealed class SseWriter : IDisposable
{
    /// <summary>Default max events written between flushes while the queue stays busy.</summary>
    public const int DefaultFlushEvery = 64;

    private readonly PipeWriter _pipe;
    private readonly ArrayBufferWriter<byte> _scratch = new(1024);
    private readonly Utf8JsonWriter _json;

    public SseWriter(PipeWriter pipe)
    {
        _pipe = pipe;
        _json = new Utf8JsonWriter(_scratch);
    }

    /// <summary>Write one event whose data is <paramref name="value"/> serialized with <paramref name="typeInfo"/>. Does not flush.</summary>
    public void Write<T>(ReadOnlySpan<byte> eventName, T value, JsonTypeInfo<T> typeInfo)
    {
        _scratch.Clear();
        _json.Reset();
        JsonSerializer.Serialize(_json, value, typeInfo);
        _json.Flush();
        Commit(eventName);
    }

    /// <summary>The final search event, <c>{"count":N}</c>. Does not flush.</summary>
    public void WriteDone(int count)
    {
        _scratch.Clear();
        _json.Reset();
        _json.WriteStartObject();
        _json.WriteNumber("count"u8, count);
        _json.WriteEndObject();
        _json.Flush();
        Commit("done"u8);
    }

    /// <summary>The final reload event, <c>{"catalogs":N,"entries":M}</c>. Does not flush.</summary>
    public void WriteDone(int catalogs, long entries)
    {
        _scratch.Clear();
        _json.Reset();
        _json.WriteStartObject();
        _json.WriteNumber("catalogs"u8, catalogs);
        _json.WriteNumber("entries"u8, entries);
        _json.WriteEndObject();
        _json.Flush();
        Commit("done"u8);
    }

    /// <summary>
    /// Flush buffered events to the client. Completes only once the transport has accepted the data, so a
    /// stalled client suspends the caller here. Returns false if the client has gone away.
    /// </summary>
    public async ValueTask<bool> FlushAsync(CancellationToken ct = default)
    {
        var result = await _pipe.FlushAsync(ct).ConfigureAwait(false);
        return !(result.IsCompleted || result.IsCanceled);
    }

    /// <summary>
    /// Drain <paramref name="reader"/> to the response: each item is written by <paramref name="write"/>, and
    /// output is flushed every <paramref name="flushEvery"/> events and whenever the queue runs dry, so nothing
    /// is held back while idle. Returns true when the reader completed (tail flushed), or false if the client
    /// went away first (the caller must not write further). Throws <see cref="OperationCanceledException"/> on
    /// cancellation.
    /// </summary>
    public async Task<bool> DrainAsync<T>(ChannelReader<T> reader, Action<SseWriter, T> write, CancellationToken ct,
        int flushEvery = DefaultFlushEvery)
    {
        while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            var pending = 0;
            while (reader.TryRead(out var item))
            {
                write(this, item);
                if (++pending >= flushEvery)
                {
                    if (!await FlushAsync(ct).ConfigureAwait(false)) return false;
                    pending = 0;
                }
            }

            if (pending > 0 && !await FlushAsync(ct).ConfigureAwait(false)) return false;
        }

        return true;
    }

    public void Dispose() => _json.Dispose();

    // Copy the finished frame into the pipe in one go.
    private void Commit(ReadOnlySpan<byte> eventName)
    {
        _pipe.Write("event: "u8);
        _pipe.Write(eventName);
        _pipe.Write("\ndata: "u8);
        _pipe.Write(_scratch.WrittenSpan);
        _pipe.Write("\n\n"u8);
    }
}
