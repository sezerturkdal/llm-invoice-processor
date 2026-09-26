using System.Threading.Channels;

namespace InvoiceProcessor.Api.Processing;

/// <summary>
/// In-memory queue of invoice ids waiting for extraction. Nothing is lost on restart: the worker
/// re-queues every invoice still in Processing when it starts.
/// </summary>
public sealed class InvoiceProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid invoiceId) => _channel.Writer.TryWrite(invoiceId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
