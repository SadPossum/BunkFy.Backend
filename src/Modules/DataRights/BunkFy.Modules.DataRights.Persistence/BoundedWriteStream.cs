namespace BunkFy.Modules.DataRights.Persistence;

internal sealed class BoundedWriteStream(Stream inner, long maximumBytes)
    : Stream
{
    private long written;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => this.written;
    public override long Position
    {
        get => this.written;
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        inner.FlushAsync(cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.Reserve(count);
        inner.Write(buffer, offset, count);
        this.written += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.Reserve(buffer.Length);
        inner.Write(buffer);
        this.written += buffer.Length;
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        this.Reserve(buffer.Length);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        this.written += buffer.Length;
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        this.Reserve(count);
        return this.WriteLegacyAsync(buffer, offset, count, cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    private async Task WriteLegacyAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken)
            .ConfigureAwait(false);
        this.written += count;
    }

    private void Reserve(int count)
    {
        if (count < 0 || this.written > maximumBytes - count)
        {
            throw new DataRightsExportBufferLimitException(
                "The protected export exceeds its configured byte limit.");
        }
    }
}

internal sealed class DataRightsExportBufferLimitException(string message)
    : IOException(message);
