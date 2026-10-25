using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FullWorth.Tests.Infrastructure;

// Disposable fixture only. Inspect protocol command tags, never credentials,
// SQL parameters, provider tokens or financial payloads. There is no remote target.
internal sealed class LoopbackCommitProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _connections = [];
    private readonly Task _accept;
    private string? _dropTag;
    private TaskCompletionSource _dropped = NewSignal();
    internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    internal int Drops { get; private set; }
    internal Task Dropped => _dropped.Task;

    internal LoopbackCommitProxy()
    {
        _listener.Start();
        _accept = AcceptAsync();
    }

    internal void Arm(string commandTag)
    {
        if (commandTag is not ("COMMIT" or "INSERT 0 1"))
            throw new InvalidOperationException("Only fixture commit acknowledgements may be dropped.");
        if (Interlocked.CompareExchange(ref _dropTag, commandTag, null) is not null)
            throw new InvalidOperationException("The fixture fault is already armed.");
        _dropped = NewSignal();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                var task = ForwardAsync(client);
                lock (_connections) _connections.Add(task);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
    }

    private async Task ForwardAsync(TcpClient client)
    {
        using (client)
        using (var server = new TcpClient())
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
        {
            try
            {
                await server.ConnectAsync(IPAddress.Loopback, 5432, linked.Token);
                var incoming = client.GetStream();
                var outgoing = server.GetStream();
                var requests = incoming.CopyToAsync(outgoing, linked.Token);
                var replies = RepliesAsync(outgoing, incoming, linked.Token);
                await Task.WhenAny(requests, replies);
                linked.Cancel();
                client.Close();
                server.Close();
                try { await Task.WhenAll(requests, replies); }
                catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        }
    }

    private async Task RepliesAsync(NetworkStream source, NetworkStream destination, CancellationToken ct)
    {
        var header = new byte[5];
        while (true)
        {
            var read = await source.ReadAsync(header.AsMemory(0, 1), ct);
            if (read == 0) return;
            await source.ReadExactlyAsync(header.AsMemory(1, 4), ct);
            var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
            if (length < 4 || length > 16 * 1024 * 1024)
                throw new InvalidOperationException("Invalid PostgreSQL fixture frame.");
            var payload = new byte[length - 4];
            await source.ReadExactlyAsync(payload, ct);
            if (header[0] == (byte)'C')
            {
                var tag = Encoding.ASCII.GetString(payload).TrimEnd('\0');
                var armed = Volatile.Read(ref _dropTag);
                if (armed == tag && Interlocked.CompareExchange(ref _dropTag, null, armed) == armed)
                {
                    // CommandComplete follows execution on the real server.
                    // Closing BOTH sockets here loses the reply, not the commit.
                    Drops++;
                    _dropped.TrySetResult();
                    return;
                }
            }
            await destination.WriteAsync(header, ct);
            await destination.WriteAsync(payload, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        await _accept;
        Task[] tasks;
        lock (_connections) tasks = _connections.ToArray();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        _stop.Dispose();
    }
}
