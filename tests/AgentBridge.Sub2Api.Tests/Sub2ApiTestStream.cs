namespace AgentBridge.Sub2Api.Tests;

/// <summary>Локальный fragmented stream с наблюдаемыми read/dispose boundaries, без сети и процессов.</summary>
internal class Sub2ApiTestStream(byte[] bytes, int fragment) : Stream
{
    private int position;
    internal bool Disposed { get; private set; }
    internal bool BlockAtEnd { get; set; }
    internal Exception? ReadFailure { get; set; }
    internal Exception? CleanupFailure { get; set; }
    internal Action? OnDispose { get; set; }
    internal TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => position; set => throw new NotSupportedException(); }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (position < bytes.Length)
        {
            int count = Math.Min(Math.Min(buffer.Length, fragment), bytes.Length - position);
            bytes.AsMemory(position, count).CopyTo(buffer);
            position += count;
            return count;
        }

        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        if (BlockAtEnd)
        {
            Blocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return 0;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        OnDispose?.Invoke();
        base.Dispose(disposing);
        if (CleanupFailure is not null)
        {
            throw CleanupFailure;
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
