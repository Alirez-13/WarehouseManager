namespace Warehouse.Core.Exceptions;

public class InsufficientStockException : Exception
{
    public InsufficientStockException(int productId, decimal available, decimal requested)
        : base($"Product ID {productId} has insufficient stock. Available: {available}, Requested: {requested}") { }
}

