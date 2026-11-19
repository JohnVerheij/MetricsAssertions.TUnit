using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

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

        // A meter factory of the test's own: on .NET 11 every handler publishes its own open_connections instrument on the
        // shared System.Net.Http meter, so a capture by name attaches to the first handler's, often another test's.
        using var meterFactory = new TestMeterFactory();
        using var capture = MeterCapture.For("System.Net.Http", meterFactory).Add<long>("http.client.open_connections");
        using var handler = new SocketsHttpHandler { MeterFactory = meterFactory };
        using var http = new HttpClient(handler);
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
            // The README sample reads the same total on both: one open connection.
            await Assert.That(capture.CounterTotal("http.client.open_connections")).IsEqualTo(1L);
#endif
        }
        finally
        {
            release.SetResult();
            await server;
        }
    }

#if NET11_0_OR_GREATER
    [Test]
    public async Task OpenConnections_OnNet11_EveryHandlerPublishesItsOwnInstrument(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            for (int i = 0; i < 2; i++)
            {
                using var client = await listener.AcceptTcpClientAsync(ct);
                await using var stream = client.GetStream();
                _ = await stream.ReadAsync(new byte[4096], ct);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), ct);
            }
        }, ct);

        using var first = new HttpClient();
        using var second = new HttpClient();
        using var r1 = await first.GetAsync(new Uri($"http://127.0.0.1:{port}/"), ct);
        using var r2 = await second.GetAsync(new Uri($"http://127.0.0.1:{port}/"), ct);
        await server;

        // A listener started now is told of every instrument already published: one per handler, all on one meter.
        var instruments = new System.Collections.Generic.List<System.Diagnostics.Metrics.Instrument>();
        using (var meterListener = new System.Diagnostics.Metrics.MeterListener())
        {
            meterListener.InstrumentPublished = (instrument, _) =>
            {
                if (instrument is { Name: "http.client.open_connections", Meter: { Name: "System.Net.Http", Scope: null } })
                    instruments.Add(instrument);
            };
            meterListener.Start();
        }

        await Assert.That(instruments.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(instruments.Select(i => i.Meter).Distinct().Count()).IsEqualTo(1);
    }
#endif

    [Test]
    public async Task RequestDuration_HasOneSeriesPerStatusCode_NotPerPath(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string[] paths = ["/orders/1", "/orders/2", "/orders/3", "/missing"];
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            for (int i = 0; i < paths.Length; i++)
            {
                using var client = await listener.AcceptTcpClientAsync(ct);
                await using var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer, ct);
                var path = Encoding.ASCII.GetString(buffer, 0, read).Split(' ')[1];
                var status = path.StartsWith("/orders/", StringComparison.Ordinal) ? "200 OK" : "404 Not Found";
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), ct);
            }
        }, ct);

        // Flow-scoped: other tests of this process use HttpClient too, and System.Net.Http is one static meter.
        using var capture = MeterCapture.For("System.Net.Http").InCurrentFlow().Add<double>("http.client.request.duration");
        using var http = new HttpClient();
        foreach (var path in paths)
        {
            using var response = await http.GetAsync(new Uri($"http://127.0.0.1:{port}{path}"), ct);
        }

        await server;
        MeasurementSet durations = capture["http.client.request.duration"].Measurements;

        await Assert.That(durations.Count).IsEqualTo(4);
        await Assert.That(durations.DistinctTagValues("http.response.status_code")).IsEquivalentTo(["200", "404"], CollectionOrdering.Matching);
        await Assert.That(durations.DistinctTagValues("server.address")).IsEquivalentTo(["127.0.0.1"]);
        await Assert.That(durations.DistinctSeries()).Count().IsEqualTo(2);
        await Assert.That(durations.DistinctSeries().Any(s => s.Contains("/orders/", StringComparison.Ordinal))).IsFalse();
    }
}
