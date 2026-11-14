using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions.Tests;

/// <summary>
/// The <c>System.Net.Http</c> connection metrics against a loopback server: <c>http.client.open_connections</c> is an
/// up-down counter on .NET 10 (its measurements arrive as the connections open and close) and an observable up-down
/// counter on .NET 11 (a capture needs <see cref="MeterCapture.RecordObservable"/> to read it).
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class HttpClientMetricsTests
{
    [Test]
    public async Task OpenConnections_AreReadAsTheRuntimeEmitsThem(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            await using var stream = client.GetStream();
            _ = await stream.ReadAsync(new byte[4096], ct);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: keep-alive\r\n\r\nok"), ct);
            await release.Task.WaitAsync(ct);
        }, ct);

        using var capture = MeterCapture.For("System.Net.Http").Add<long>("http.client.open_connections");
        using var http = new HttpClient();
        var body = await http.GetStringAsync(new Uri($"http://127.0.0.1:{port}/"), ct);
        var beforePull = capture.MeasurementCount("http.client.open_connections");
        capture.RecordObservable();

        // Assert while the server still holds the connection: once it closes, .NET 10 records the close as a measurement.
        try
        {
            await Assert.That(body).IsEqualTo("ok");
#if NET11_0_OR_GREATER
            // Observable on .NET 11: nothing is recorded until RecordObservable pulls the current value.
            await Assert.That(beforePull).IsEqualTo(0);
            await Assert.That(capture.CounterTotal("http.client.open_connections")).IsEqualTo(1L);
            await Assert.That(capture["http.client.open_connections"].Measurements.All.All(m => m.Tags.ContainsKey("http.connection.state"))).IsTrue();
#else
            // An up-down counter before .NET 11: the measurements were recorded while the request ran.
            await Assert.That(beforePull).IsGreaterThan(0);
            await Assert.That(capture.MeasurementCount("http.client.open_connections")).IsEqualTo(beforePull);
#endif
        }
        finally
        {
            release.SetResult();
            await server;
        }
    }
}
