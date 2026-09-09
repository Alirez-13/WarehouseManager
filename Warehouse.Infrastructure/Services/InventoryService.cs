namespace Warehouse.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Core.Commands;
using Core.Entities;
using Core.Exceptions;
using Core.Interfaces;
using Persistence;

public class InventoryService : IInventoryService
{
    private readonly WarehouseDbContext _context;

    public InventoryService(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task ProcessInboundReceiptAsync(CreateInboundReceiptCommand cmd, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var inbound = new InboundTransaction
            {
                ReferenceNumber = cmd.ReferenceNumber,
                SupplierName = cmd.SupplierName,
                TransactionDateUtc = DateTime.UtcNow,
                Notes = cmd.Notes
            };

            decimal totalAmount = 0;

            foreach (var item in cmd.Items)
            {
                var product = await _context.Products
                                  .Include(p => p.InventoryItem)
                                  .FirstOrDefaultAsync(p => p.Id == item.ProductId, ct)
                              ?? throw new InvalidOperationException($"Product ID {item.ProductId} does not exist.");

                // Initialize stock item if not present
                if (product.InventoryItem == null)
                {
                    product.InventoryItem = new InventoryItem
                    {
                        ProductId = product.Id,
                        QuantityOnHand = 0
                    };
                    _context.InventoryItems.Add(product.InventoryItem);
                }

                // Increment stock
                product.InventoryItem.QuantityOnHand += item.Quantity;

                var lineTotal = item.Quantity * item.UnitPrice;
                totalAmount += lineTotal;

                inbound.Lines.Add(new InboundTransactionLine
                {
                    ProductId = product.Id,
                    ProductSkuSnapshot = product.Sku,
                    ProductNameSnapshot = product.Name,
                    UnitSnapshot = product.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = lineTotal
                });
            }

            inbound.TotalAmount = totalAmount;
            _context.InboundTransactions.Add(inbound);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(ct);
            throw new ConcurrencyConflictException("Stock was updated concurrently. Inbound operation cancelled.", ex);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ProcessOutboundSaleAsync(CreateOutboundSaleCommand cmd, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Auto-provision or link Customer permanently
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == cmd.CustomerPhone, ct);
            if (customer == null)
            {
                customer = new Customer
                {
                    Name = cmd.CustomerName,
                    Phone = cmd.CustomerPhone,
                    Address = cmd.CustomerAddress
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync(ct);
            }

            var outbound = new OutboundTransaction
            {
                InvoiceNumber = cmd.InvoiceNumber,
                CustomerId = customer.Id,
                CustomerNameSnapshot = customer.Name,
                CustomerPhoneSnapshot = customer.Phone,
                TransactionDateUtc = DateTime.UtcNow,
                Notes = cmd.Notes
            };

            decimal totalAmount = 0;

            foreach (var item in cmd.Items)
            {
                var stock = await _context.InventoryItems
                    .Include(i => i.Product)
                    .FirstOrDefaultAsync(i => i.ProductId == item.ProductId, ct);

                if (stock == null || stock.QuantityOnHand < item.Quantity)
                {
                    var available = stock?.QuantityOnHand ?? 0m;
                    throw new InsufficientStockException(item.ProductId, available, item.Quantity);
                }

                // Decrement stock
                stock.QuantityOnHand -= item.Quantity;

                var lineTotal = item.Quantity * item.UnitPrice;
                totalAmount += lineTotal;

                outbound.Lines.Add(new OutboundTransactionLine
                {
                    ProductId = stock.ProductId,
                    ProductSkuSnapshot = stock.Product.Sku,
                    ProductNameSnapshot = stock.Product.Name,
                    UnitSnapshot = stock.Product.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = lineTotal
                });
            }

            outbound.TotalAmount = totalAmount;
            _context.OutboundTransactions.Add(outbound);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(ct);
            throw new ConcurrencyConflictException("Another transaction modified product inventory. Please retry.", ex);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}