using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public enum ClipboardOperationKind
    {
        Copy,
        Move
    }

    public sealed class ClipboardData
    {
        // Пути к объектам (могут быть из разных провайдеров!)
        public IReadOnlyList<string> SourcePaths { get; init; } = Array.Empty<string>();

        // Тип операции: Copy или Move
        public ClipboardOperationKind Operation { get; init; }

        // Время помещения в буфер (удобно для отладки, логов, UI)
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        public bool IsEmpty => SourcePaths.Count == 0;

    }
}
