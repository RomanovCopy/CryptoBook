namespace CryptoBook.Interfaces;

public enum TableEditOperation
{
    InsertRowAbove,
    InsertRowBelow,
    InsertColumnLeft,
    InsertColumnRight,
    DeleteRow,
    DeleteColumn
}

public interface ITableEditingService
{
    bool CanEdit { get; }
    bool Edit(TableEditOperation operation);
}
