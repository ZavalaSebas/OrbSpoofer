using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace OrbSpoofer.Services;

public sealed class DiscordIpc : IDisposable
{
    private const int OpHandshake = 0;
    private const int OpFrame = 1;
    private const int OpPing = 3;
    private const int OpPong = 4;

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    public static DiscordIpc? TryConnect(string applicationId, string? gameName = null)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
            return null;

        for (var i = 0; i < 10; i++)
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                    PipeDirection.InOut, PipeOptions.Asynchronous);
                pipe.Connect(200);
                var ipc = new DiscordIpc { _pipe = pipe };
                ipc.Handshake(applicationId);
                ipc.SetActivity(gameName);
                ipc.StartReadLoop();
                return ipc;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discord IPC pipe {i} failed: {ex.Message}");
            }
        }

        return null;
    }

    private void Handshake(string clientId)
    {
        Write(OpHandshake, new JsonObject { ["v"] = 1, ["client_id"] = clientId }.ToJsonString());
    }

    private void SetActivity(string? gameName)
    {
        var activity = new JsonObject
        {
            ["type"] = 0,
            ["timestamps"] = new JsonObject
            {
                ["start"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };
        if (!string.IsNullOrWhiteSpace(gameName))
            activity["details"] = gameName;

        var frame = new JsonObject
        {
            ["cmd"] = "SET_ACTIVITY",
            ["nonce"] = Guid.NewGuid().ToString(),
            ["args"] = new JsonObject
            {
                ["pid"] = Environment.ProcessId,
                ["activity"] = activity
            }
        };
        Write(OpFrame, frame.ToJsonString());
    }

    private void StartReadLoop()
    {
        _cts = new CancellationTokenSource();
        _readLoop = Task.Run(() => ReadLoop(_cts.Token));
    }

    private void ReadLoop(CancellationToken token)
    {
        var header = new byte[8];
        while (!token.IsCancellationRequested && _pipe is { IsConnected: true })
        {
            try
            {
                if (!ReadExact(header, token))
                    break;
                var op = BitConverter.ToInt32(header, 0);
                var len = BitConverter.ToInt32(header, 4);
                if (len < 0 || len > 1_000_000)
                    break;
                var body = new byte[len];
                if (len > 0 && !ReadExact(body, token))
                    break;
                if (op == OpPing)
                    Write(OpPong, Encoding.UTF8.GetString(body));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discord IPC read failed: {ex.Message}");
                break;
            }
        }
    }

    private bool ReadExact(byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            token.ThrowIfCancellationRequested();
            var n = _pipe!.Read(buffer, offset, buffer.Length - offset);
            if (n == 0) return false;
            offset += n;
        }
        return true;
    }

    private void Write(int opcode, string json)
    {
        if (_pipe is not { IsConnected: true }) return;
        var body = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(body.Length).CopyTo(header, 4);
        _pipe.Write(header, 0, 8);
        _pipe.Write(body, 0, body.Length);
        _pipe.Flush();
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _readLoop?.Wait(500); } catch { /* ignore */ }
        _cts?.Dispose();
        _pipe?.Dispose();
    }
}
