using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Security
{
    public sealed class HmacWriteStream: Stream
    {
        private readonly Stream _baseStream;
        private readonly IncrementalHash _hmac;
        private readonly bool _leaveOpen;

        private bool _disposed;

        public HmacWriteStream( Stream baseStream, byte[] key, bool leaveOpen)
        {
            _baseStream = baseStream;
            _leaveOpen = leaveOpen;

            _hmac = IncrementalHash.CreateHMAC( HashAlgorithmName.SHA256, key);
        }

        public byte[] GetHashAndReset()
        {
            return _hmac.GetHashAndReset();
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write( byte[] buffer, int offset, int count)
        {
            _hmac.AppendData(buffer, offset, count);

            _baseStream.Write(buffer, offset, count);
        }

        public override async ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                buffer.CopyTo(rented);

                _hmac.AppendData(rented, 0, buffer.Length);

                await _baseStream.WriteAsync(
                    rented.AsMemory(0, buffer.Length),
                    cancellationToken);
            } finally
            {
                CryptographicOperations.ZeroMemory(
                    rented.AsSpan(0, buffer.Length));

                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override Task FlushAsync( CancellationToken cancellationToken)
        {
            return _baseStream.FlushAsync(cancellationToken);
        }

        public override int Read( byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek( long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if(_disposed)
            {
                return;
            }

            if(disposing)
            {
                _hmac.Dispose();

                if(!_leaveOpen)
                {
                    _baseStream.Dispose();
                }
            }

            _disposed = true;

            base.Dispose(disposing);
        }
    }
}
