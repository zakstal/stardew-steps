using System.Net;
using System.Net.Sockets;

namespace StardewSteps.Core;

/// <summary>
/// Localhost TCP listener. Each "step" line calls onStep (on a background thread).
/// "ping" and unknown lines are ignored. A new client replaces the previous one.
/// </summary>
public sealed class StepServer
{
    private readonly TcpListener _listener;
    private readonly Action _onStep;
    private readonly Action<string> _log;
    private readonly object _lock = new();
    private TcpClient? _current;
    private Thread? _acceptThread;
    private volatile bool _running;

    public StepServer(IPAddress addr, int port, Action onStep, Action<string> log)
    {
        _listener = new TcpListener(addr, port);
        _onStep = onStep;
        _log = log;
    }

    public int BoundPort => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        _running = true;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "StardewSteps.Accept" };
        _acceptThread.Start();
        _log($"Listening on {_listener.LocalEndpoint}");
    }

    public void Stop()
    {
        _running = false;
        try { _listener.Stop(); } catch { }
        lock (_lock) { _current?.Close(); _current = null; }
        _acceptThread?.Join(1000);
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client;
            try { client = _listener.AcceptTcpClient(); }
            catch { if (!_running) return; continue; }

            lock (_lock)
            {
                if (_current != null) _log("New bridge connection; closing previous one");
                _current?.Close();
                _current = client;
            }
            _log("Bridge connected");
            new Thread(() => ReadLoop(client)) { IsBackground = true, Name = "StardewSteps.Read" }.Start();
        }
    }

    private void ReadLoop(TcpClient client)
    {
        var parser = new LineParser();
        var buf = new byte[256];
        try
        {
            var stream = client.GetStream();
            int n;
            while ((n = stream.Read(buf, 0, buf.Length)) > 0)
            {
                foreach (var line in parser.Feed(buf.AsSpan(0, n)))
                    if (line == "step") _onStep();
            }
        }
        catch { /* closed or reset */ }
        finally
        {
            client.Close();
            lock (_lock) { if (_current == client) { _current = null; _log("Bridge disconnected"); } }
        }
    }
}
