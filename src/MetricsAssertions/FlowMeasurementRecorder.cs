using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsAssertions;

/// <summary>
/// Records the measurements of the matching instruments that were emitted in one async flow (see
/// <see cref="FlowContext"/>), with an own <see cref="MeterListener"/>. The listener's callback runs synchronously on the
/// recording thread inside its execution context, so the flow marker is readable there and measurements of other flows are
/// dropped (and counted). Every matching instrument is subscribed, not the first one only.
/// </summary>
/// <typeparam name="T">The value type of the captured instruments.</typeparam>
internal sealed class FlowMeasurementRecorder<T> : IDisposable where T : struct
{
    private readonly object _token;
    private readonly string _instrumentName;
    private readonly TimeProvider _timeProvider;
    private readonly MeterListener _listener = new();
    private readonly Lock _gate = new();
    private readonly List<CapturedMeasurement> _measurements = [];
    private readonly List<Waiter> _waiters = [];
    private int _ignored;
    private bool _disposed;

    /// <summary>Starts recording the flow identified by <paramref name="token"/>.</summary>
    /// <param name="token">The token of the flow to record, from <see cref="FlowContext.Enter"/>.</param>
    /// <param name="instrumentName">The instrument name each captured measurement carries.</param>
    /// <param name="matches">Decides which published instruments are captured.</param>
    /// <param name="timeProvider">The clock for the measurement timestamps, or <see langword="null"/> for the system clock.</param>
    internal FlowMeasurementRecorder(object token, string instrumentName, Func<Instrument, bool> matches, TimeProvider? timeProvider)
    {
        _token = token;
        _instrumentName = instrumentName;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (matches(instrument))
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<T>(OnMeasurement);
        _listener.Start();
    }

    /// <summary>Gets how many measurements of the matching instruments were ignored because another flow emitted them.</summary>
    internal int IgnoredFromOtherFlows => Volatile.Read(ref _ignored);

    /// <summary>Returns a copy of the measurements recorded so far, in arrival order.</summary>
    /// <returns>The recorded measurements.</returns>
    /// <exception cref="ObjectDisposedException">The recorder was disposed, as reading a disposed <c>MetricCollector</c> does.</exception>
    internal IReadOnlyList<CapturedMeasurement> Snapshot()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return [.. _measurements];
        }
    }

    /// <summary>Pulls the current values of the matching observable instruments, in the calling flow.</summary>
    /// <exception cref="ObjectDisposedException">The recorder was disposed.</exception>
    internal void RecordObservable()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);
        _listener.RecordObservableInstruments();
    }

    /// <summary>Waits until at least <paramref name="count"/> measurements were recorded.</summary>
    /// <param name="count">The minimum number of recorded measurements.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns>A task that completes when the count is reached, or is canceled with the token.</returns>
    internal Task WaitAsync(int count, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiter = new Waiter(count, completion, cancellationToken);
        lock (_gate)
        {
            if (_measurements.Count >= count)
                return Task.CompletedTask;
            _waiters.Add(waiter);
        }

        if (cancellationToken.CanBeCanceled)
            waiter.Registration = cancellationToken.Register(() => Cancel(waiter));

        return completion.Task;
    }

    /// <summary>Stops the listener: no measurement is recorded after this call.</summary>
    public void Dispose()
    {
        lock (_gate)
            _disposed = true;
        _listener.Dispose();
    }

    /// <summary>Removes a pending wait and cancels it.</summary>
    /// <param name="waiter">The wait whose token was canceled.</param>
    private void Cancel(Waiter waiter)
    {
        lock (_gate)
            _waiters.Remove(waiter);
        waiter.Completion.TrySetCanceled(waiter.Token);
    }

    private void OnMeasurement(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (!FlowContext.Contains(_token))
        {
            Interlocked.Increment(ref _ignored);
            return;
        }

        var tagMap = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> tag in tags)
            tagMap[tag.Key] = tag.Value;

        var captured = new CapturedMeasurement(
            _instrumentName,
            Convert.ToDouble(measurement, CultureInfo.InvariantCulture),
            tagMap,
            _timeProvider.GetUtcNow());

        List<Waiter>? ready = null;
        lock (_gate)
        {
            _measurements.Add(captured);
            for (int i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_waiters[i].Count > _measurements.Count)
                    continue;
                (ready ??= []).Add(_waiters[i]);
                _waiters.RemoveAt(i);
            }
        }

        if (ready is null)
            return;

        foreach (Waiter waiter in ready)
        {
            waiter.Registration.Unregister();
            waiter.Completion.TrySetResult();
        }
    }

    /// <summary>A pending <see cref="WaitAsync"/>: the count it waits for, its completion and its cancellation.</summary>
    /// <param name="count">The measurement count that completes the wait.</param>
    /// <param name="completion">The completion of the wait.</param>
    /// <param name="token">The token that cancels the wait.</param>
    private sealed class Waiter(int count, TaskCompletionSource completion, CancellationToken token)
    {
        /// <summary>Gets the measurement count that completes the wait.</summary>
        internal int Count { get; } = count;

        /// <summary>Gets the completion of the wait.</summary>
        internal TaskCompletionSource Completion { get; } = completion;

        /// <summary>Gets the token that cancels the wait.</summary>
        internal CancellationToken Token { get; } = token;

        /// <summary>Gets or sets the registration that cancels the wait with the caller's token.</summary>
        internal CancellationTokenRegistration Registration { get; set; }
    }
}
