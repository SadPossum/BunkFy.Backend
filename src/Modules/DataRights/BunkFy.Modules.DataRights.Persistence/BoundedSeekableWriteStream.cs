namespace BunkFy.Modules.DataRights.Persistence;

internal sealed class BoundedSeekableWriteStream(
    Stream inner,
    long maximumBytes) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position
    {
        get => inner.Position;
        set
        {
            this.ValidatePosition(value);
            inner.Position = value;
        }
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => inner.Read(buffer);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.Reserve(count);
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.Reserve(buffer.Length);
        inner.Write(buffer);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        this.Reserve(buffer.Length);
        return inner.WriteAsync(buffer, cancellationToken);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        this.Reserve(count);
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(inner.Position + offset),
            SeekOrigin.End => checked(inner.Length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        this.ValidatePosition(position);
        return inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        this.ValidatePosition(value);
        inner.SetLength(value);
    }

    private void Reserve(int count)
    {
        if (count < 0 || inner.Position > maximumBytes - count)
        {
            throw new DataRightsExportBufferLimitException(
                "The protected export exceeds its configured byte limit.");
        }
    }

    private void ValidatePosition(long value)
    {
        if (value < 0 || value > maximumBytes)
        {
            throw new DataRightsExportBufferLimitException(
                "The protected export exceeds its configured byte limit.");
        }
    }
}
