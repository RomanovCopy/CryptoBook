using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Channels;

namespace CryptoBook.Services;

/// <summary>A stable, read-only view. Neither passwords nor decoded file data are written to disk.</summary>
internal sealed class ArchiveReadSession: IDisposable
{
    private readonly ArchiveService service;
    private readonly IDisposable sourceLock;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SecureString? password;
    private readonly object gate = new();
    private bool disposed;

    internal ArchiveReadSession(ArchiveService service, IDisposable sourceLock, string archivePath,
        string type, IReadOnlyList<ArchiveService.Entry> entries, string? password)
    {
        this.service = service;
        this.sourceLock = sourceLock;
        ArchivePath = archivePath;
        Type = type;
        Entries = entries;
        if(!string.IsNullOrEmpty(password))
        {
            this.password = new SecureString();
            foreach(char character in password) this.password.AppendChar(character);
            this.password.MakeReadOnly();
        }
    }

    internal string ArchivePath { get; }
    internal string Type { get; }
    internal IReadOnlyList<ArchiveService.Entry> Entries { get; }
    internal CancellationTokenSource CreateCancellation(CancellationToken token)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        }
    }

    internal string? GetPassword()
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(password is null) return null;
            IntPtr pointer = Marshal.SecureStringToGlobalAllocUnicode(password);
            try { return Marshal.PtrToStringUni(pointer); }
            finally { Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
        }
    }

    internal Stream OpenRead(ArchiveService.Entry entry, CancellationToken token, bool passwordProbe = false)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(entry.IsDirectory) throw new IOException(ArchivePathPolicy.Text("NotAFile"));
            if(!passwordProbe && entry.Size > new DTO.ArchiveExtractionLimits().MaximumBytes)
                throw new IOException(ArchivePathPolicy.Text("ExtractionLimit"));
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            return new ArchiveEntryReadStream(entry.Size, cancellation,
                (output, ct) => service.ReadEntryAsync(this, entry, output, ct));
        }
    }

    public void Dispose()
    {
        lock(gate)
        {
            if(disposed) return;
            disposed = true;
            lifetime.Cancel();
            sourceLock.Dispose();
            password?.Dispose();
            // Outstanding readers own linked registrations until they are disposed.
            lifetime.Dispose();
        }
    }
}

/// <summary>Bounded streaming with backpressure; EOF waits for the native CRC/exit result.</summary>
internal sealed class ArchiveEntryReadStream: Stream
{
    private readonly Channel<byte[]> chunks = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(2)
    {
        SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait
    });
    private readonly CancellationTokenSource cancellation;
    private readonly Task producer;
    private readonly long length;
    private byte[]? current;
    private int offset;
    private long position;
    private bool disposed;

    internal ArchiveEntryReadStream(long length, CancellationTokenSource cancellation,
        Func<Stream, CancellationToken, Task> produce)
    {
        this.length = length;
        this.cancellation = cancellation;
        producer = Task.Run(async () =>
        {
            try
            {
                using var output = new ChunkWriter(chunks.Writer);
                await produce(output, cancellation.Token).ConfigureAwait(false);
                chunks.Writer.TryComplete();
            }
            catch(Exception exception) { chunks.Writer.TryComplete(exception); throw; }
        });
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if(buffer.Length == 0) return 0;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            while(current is null || offset == current.Length)
            {
                current = null;
                offset = 0;
                if(!await chunks.Reader.WaitToReadAsync(linked.Token).ConfigureAwait(false))
                {
                    await producer.ConfigureAwait(false);
                    return 0;
                }
                if(chunks.Reader.TryRead(out byte[]? next)) current = next;
            }
            int count = Math.Min(buffer.Length, current.Length - offset);
            current.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            position += count;
            if(position == length) await producer.ConfigureAwait(false);
            return count;
        }
        catch
        {
            cancellation.Cancel();
            // Preserve the original native failure instead of ChannelClosedException.
            try { await producer.ConfigureAwait(false); }
            catch(OperationCanceledException) when(!linked.IsCancellationRequested) { throw; }
            throw;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
        ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
    protected override void Dispose(bool disposing)
    {
        if(disposing && !disposed)
        {
            disposed = true;
            cancellation.Cancel();
            try { producer.GetAwaiter().GetResult(); }
            catch { /* An incomplete preview cancels its reader. EOF reports native failures. */ }
            current = null;
            while(chunks.Reader.TryRead(out _)) { }
            cancellation.Dispose();
        }
        base.Dispose(disposing);
    }
    public override bool CanRead => !disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private sealed class ChunkWriter(ChannelWriter<byte[]> writer): Stream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) =>
            await writer.WriteAsync(buffer.ToArray(), token).ConfigureAwait(false);
        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
