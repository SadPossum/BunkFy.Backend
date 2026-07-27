namespace BunkFy.Modules.DataRights.Persistence;

using System.Security.Cryptography;

internal sealed class HashingNullWriteStream : Stream
{
    private readonly IncrementalHash hash =
        IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool completed;
    private long length;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !this.completed;
    public override long Length => this.length;
    public override long Position
    {
        get => this.Length;
        set => throw new NotSupportedException();
    }

    public byte[] CompleteHash()
    {
        ObjectDisposedException.ThrowIf(this.completed, this);
        this.completed = true;
        return this.hash.GetHashAndReset();
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override void Write(byte[] buffer, int offset, int count)
    {
        this.EnsureWritable();
        this.hash.AppendData(buffer, offset, count);
        this.length = checked(this.length + count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.EnsureWritable();
        this.hash.AppendData(buffer);
        this.length = checked(this.length + buffer.Length);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.hash.Dispose();
            this.completed = true;
        }

        base.Dispose(disposing);
    }

    private void EnsureWritable()
    {
        ObjectDisposedException.ThrowIf(this.completed, this);
    }
}
