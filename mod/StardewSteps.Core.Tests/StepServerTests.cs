using System.Net;
using System.Net.Sockets;
using System.Text;
using StardewSteps.Core;
using Xunit;

namespace StardewSteps.Core.Tests;

public class StepServerTests
{
    private static async Task WaitFor(Func<bool> cond, int ms = 2000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (!cond() && DateTime.UtcNow < until) await Task.Delay(10);
    }

    [Fact]
    public async Task CountsOnlyStepLines()
    {
        int steps = 0;
        var server = new StepServer(IPAddress.Loopback, 0, () => Interlocked.Increment(ref steps), _ => { });
        server.Start();
        try
        {
            using var c = new TcpClient();
            await c.ConnectAsync(IPAddress.Loopback, server.BoundPort);
            await c.GetStream().WriteAsync(Encoding.ASCII.GetBytes("step\nping\nhello\nstep\n"));
            await WaitFor(() => Volatile.Read(ref steps) >= 2);
            await Task.Delay(100);
            Assert.Equal(2, Volatile.Read(ref steps));
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task NewClientReplacesOld()
    {
        int steps = 0;
        var server = new StepServer(IPAddress.Loopback, 0, () => Interlocked.Increment(ref steps), _ => { });
        server.Start();
        try
        {
            using var a = new TcpClient();
            await a.ConnectAsync(IPAddress.Loopback, server.BoundPort);
            await Task.Delay(100);
            using var b = new TcpClient();
            await b.ConnectAsync(IPAddress.Loopback, server.BoundPort);

            var buf = new byte[16];
            var readTask = a.GetStream().ReadAsync(buf, 0, buf.Length);
            var finished = await Task.WhenAny(readTask, Task.Delay(2000));
            Assert.Same(readTask, finished);
            Assert.Equal(0, await readTask); // EOF: old client closed

            await b.GetStream().WriteAsync(Encoding.ASCII.GetBytes("step\n"));
            await WaitFor(() => Volatile.Read(ref steps) >= 1);
            Assert.Equal(1, Volatile.Read(ref steps));
        }
        finally { server.Stop(); }
    }
}
