using System.Collections.Concurrent;
using BuildSoft.OscCore;
using TimecodeBridge.Models;
using TimecodeBridge.Services.Interfaces;

namespace TimecodeBridge.Services;

/// <summary>
/// Production implementation of IOscTransport using BuildSoft.OscCore's OscClient for UDP sending.
/// Clients are cached per (ip, port) to avoid the per-send UDP socket create/destroy overhead,
/// which is the dominant jitter source for fixed-rate relay sending.
/// </summary>
public class OscTransport : IOscTransport, IDisposable
{
    private readonly ConcurrentDictionary<(string Ip, int Port), OscClient> _clients = new();
    private volatile bool _disposed;

    public void Send(string ipAddress, int port, string oscAddress, IReadOnlyList<OscArgument> arguments)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OscTransport));

        var client = _clients.GetOrAdd((ipAddress, port), static key => new OscClient(key.Ip, key.Port));

        if (arguments.Count == 0)
        {
            client.Send(oscAddress);
            return;
        }

        if (arguments.Count == 1)
        {
            SendSingleArgument(client, oscAddress, arguments[0]);
            return;
        }

        // For multiple arguments, send each as a separate single-argument message
        // since OscCore does not provide a built-in multi-argument send API.
        // If multi-argument messages are needed, use OscWriter directly.
        foreach (var arg in arguments)
        {
            SendSingleArgument(client, oscAddress, arg);
        }
    }

    private static void SendSingleArgument(OscClient client, string oscAddress, OscArgument argument)
    {
        switch (argument)
        {
            case OscInt32Argument intArg:
                client.Send(oscAddress, intArg.Value);
                break;
            case OscFloat32Argument floatArg:
                client.Send(oscAddress, floatArg.Value);
                break;
            case OscStringArgument stringArg:
                client.Send(oscAddress, stringArg.Value);
                break;
            default:
                throw new ArgumentException($"Unsupported OscArgument type: {argument.GetType().Name}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var client in _clients.Values)
        {
            try { client.Dispose(); } catch { /* swallow per-client errors during shutdown */ }
        }
        _clients.Clear();
        GC.SuppressFinalize(this);
    }
}
