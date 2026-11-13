using System.Threading;

namespace MetricsAssertions;

/// <summary>
/// Marks the async flow of the code that creates a flow-scoped capture. <see cref="Enter"/> pushes an opaque token onto
/// an <see cref="AsyncLocal{T}"/> chain of the calling flow, and <see cref="Contains"/> asks whether the flow that is
/// recording a measurement carries a token. The chain (rather than one id) keeps every capture created in a flow
/// visible after another capture was created in the same flow, and keeps the captures of parallel flows apart: a
/// token is in the chain of the flow that created it and of the flows started from that flow afterwards, nowhere else.
/// </summary>
internal static class FlowContext
{
    private static readonly AsyncLocal<FlowLink?> Current = new();

    /// <summary>Creates a token and adds it to the chain of the current flow.</summary>
    /// <returns>The token that identifies the calling flow for one capture.</returns>
    internal static object Enter()
    {
        var token = new object();
        Current.Value = new FlowLink(token, Current.Value);
        return token;
    }

    /// <summary>Returns whether the current flow carries <paramref name="token"/>.</summary>
    /// <param name="token">A token returned by <see cref="Enter"/>.</param>
    /// <returns><see langword="true"/> when the current flow is the flow that created the token, or one started from it afterwards.</returns>
    internal static bool Contains(object token)
    {
        for (FlowLink? link = Current.Value; link is not null; link = link.Parent)
        {
            if (ReferenceEquals(link.Token, token))
                return true;
        }

        return false;
    }

    /// <summary>One entry of the chain: a token and the chain of the flow it was added to.</summary>
    /// <param name="Token">The token of one capture.</param>
    /// <param name="Parent">The chain before the token was added, or <see langword="null"/>.</param>
    private sealed record FlowLink(object Token, FlowLink? Parent);
}
