using AutoMapper;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _current;
    private readonly IEmailService _email;

    public OrderService(IUnitOfWork uow, IMapper mapper, ICurrentUserService current, IEmailService email)
    {
        _uow = uow;
        _mapper = mapper;
        _current = current;
        _email = email;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        // Resolve item list — either from request or from current cart.
        var items = request.Items?.ToList() ?? new List<CreateOrderItemRequest>();
        if (items.Count == 0)
        {
            var cart = await GetActiveCartAsync(ct);
            if (cart == null || cart.Items.Count == 0)
                throw new AppException("Səbət boşdur.", 400);
            items = cart.Items
                .Select(i => new CreateOrderItemRequest(i.ProductVariantId, i.Quantity))
                .ToList();
        }

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var variantIds = items.Select(i => i.ProductVariantId).Distinct().ToList();
            var variants = await _uow.ProductVariants.Query()
                .Include(v => v.Product)
                .Include(v => v.Color)
                .Include(v => v.Size)
                .Where(v => variantIds.Contains(v.Id))
                .ToListAsync(ct);

            if (variants.Count != variantIds.Count)
                throw new NotFoundException("Variant");

            var order = new Order
            {
                OrderNumber = await GenerateOrderNumberAsync(ct),
                UserId = _current.UserId,
                CustomerFullName = request.CustomerFullName.Trim(),
                CustomerEmail = request.CustomerEmail.Trim().ToLowerInvariant(),
                CustomerPhone = request.CustomerPhone.Trim(),
                DeliveryAddress = request.DeliveryAddress.Trim(),
                PaymentMethod = request.PaymentMethod,
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.Pending,
                Notes = request.Notes
            };

            decimal total = 0m;
            foreach (var line in items)
            {
                var v = variants.First(x => x.Id == line.ProductVariantId);
                if (!v.IsActive || !v.Product.IsActive)
                    throw new ConflictException($"Məhsul aktiv deyil: {v.Product.NameAz}");
                if (v.StockQuantity < line.Quantity)
                    throw new ConflictException($"Kifayət qədər stok yoxdur: {v.Product.NameAz} ({v.Color.NameAz}/{v.Size.Name})");

                var unitPrice = v.Product.EffectivePrice + v.PriceAdjustment;
                var lineTotal = unitPrice * line.Quantity;

                order.Items.Add(new OrderItem
                {
                    ProductVariantId = v.Id,
                    ProductName = v.Product.NameAz,
                    ColorName = v.Color.NameAz,
                    SizeName = v.Size.Name,
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = lineTotal
                });
                total += lineTotal;

                v.StockQuantity -= line.Quantity;
                _uow.ProductVariants.Update(v);
            }

            order.TotalAmount = total;
            await _uow.Orders.AddAsync(order, ct);
            await _uow.SaveChangesAsync(ct);

            // Clear cart if order came from cart
            if (request.Items == null || request.Items.Count == 0)
            {
                var cart = await GetActiveCartAsync(ct);
                if (cart != null)
                {
                    foreach (var ci in cart.Items.ToList())
                        _uow.CartItems.Remove(ci);
                    await _uow.SaveChangesAsync(ct);
                }
            }

            await _uow.CommitTransactionAsync(ct);

            var fresh = await LoadOrderAsync(order.Id, ct);

            // Fire-and-forget order confirmation email.  Build the HTML now
            // (while we have the data), then send without awaiting so a slow
            // SMTP server never delays the checkout response.  SendAsync
            // swallows all errors internally.
            if (!string.IsNullOrWhiteSpace(fresh.CustomerEmail))
            {
                var html = EmailTemplates.OrderConfirmation(fresh);
                _ = _email.SendAsync(
                    fresh.CustomerEmail,
                    fresh.CustomerFullName,
                    $"Sifariş təsdiqi · {fresh.OrderNumber}",
                    html);
            }

            return _mapper.Map<OrderDto>(fresh);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<List<OrderDto>> ListMineAsync(CancellationToken ct = default)
    {
        if (_current.UserId is null) throw new UnauthorizedException();

        var orders = await _uow.Orders.Query()
            .Include(o => o.Items)
            .Where(o => o.UserId == _current.UserId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return _mapper.Map<List<OrderDto>>(orders);
    }

    public async Task<PaginatedList<OrderDto>> ListAllAsync(OrderQueryParameters p, CancellationToken ct = default)
    {
        var query = _uow.Orders.Query().Include(o => o.Items).AsQueryable();

        // ── Filters ───────────────────────────────────────────────────────
        if (p.Status.HasValue) query = query.Where(o => o.Status == p.Status.Value);
        if (p.PaymentStatus.HasValue) query = query.Where(o => o.PaymentStatus == p.PaymentStatus.Value);
        if (p.PaymentMethod.HasValue) query = query.Where(o => o.PaymentMethod == p.PaymentMethod.Value);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(s) ||
                o.CustomerFullName.Contains(s) ||
                o.CustomerEmail.Contains(s) ||
                o.CustomerPhone.Contains(s));
        }

        if (p.DateFrom.HasValue)
        {
            var from = p.DateFrom.Value.Date;
            query = query.Where(o => o.CreatedAt >= from);
        }
        if (p.DateTo.HasValue)
        {
            // Include the whole "to" day by taking the start of the next day.
            var toExclusive = p.DateTo.Value.Date.AddDays(1);
            query = query.Where(o => o.CreatedAt < toExclusive);
        }

        if (p.MinTotal.HasValue) query = query.Where(o => o.TotalAmount >= p.MinTotal.Value);
        if (p.MaxTotal.HasValue) query = query.Where(o => o.TotalAmount <= p.MaxTotal.Value);

        // ── Sort ──────────────────────────────────────────────────────────
        query = (p.Sort?.ToLowerInvariant()) switch
        {
            "oldest" => query.OrderBy(o => o.CreatedAt),
            "total_desc" => query.OrderByDescending(o => o.TotalAmount),
            "total_asc" => query.OrderBy(o => o.TotalAmount),
            "status" => query.OrderBy(o => o.Status).ThenByDescending(o => o.CreatedAt),
            _ => query.OrderByDescending(o => o.CreatedAt), // newest (default)
        };

        // ── Pagination ────────────────────────────────────────────────────
        var page = Math.Max(1, p.Page);
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PaginatedList<OrderDto>(_mapper.Map<List<OrderDto>>(items), total, page, pageSize);
    }

    public async Task<OrderDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, ct);
        return _mapper.Map<OrderDto>(order);
    }

    public async Task<OrderDto> UpdateStatusAsync(int id, UpdateOrderStatusRequest request, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetByIdAsync(id, ct) ?? throw new NotFoundException("Sifariş");
        order.Status = request.Status;
        if (request.PaymentStatus.HasValue) order.PaymentStatus = request.PaymentStatus.Value;
        order.UpdatedAt = DateTime.UtcNow;
        _uow.Orders.Update(order);
        await _uow.SaveChangesAsync(ct);

        var fresh = await LoadOrderAsync(id, ct);
        return _mapper.Map<OrderDto>(fresh);
    }

    private async Task<Order> LoadOrderAsync(int id, CancellationToken ct)
    {
        return await _uow.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Sifariş");
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"ORD-{year}-";
        var last = await _uow.Orders.Query()
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.Id)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);
        var seq = 1;
        if (last is not null)
        {
            var part = last.Substring(prefix.Length);
            if (int.TryParse(part, out var n)) seq = n + 1;
        }
        return $"{prefix}{seq:D6}";
    }

    private async Task<Cart?> GetActiveCartAsync(CancellationToken ct)
    {
        var q = _uow.Carts.Query().Include(c => c.Items);
        if (_current.UserId is int uid)
            return await q.FirstOrDefaultAsync(c => c.UserId == uid, ct);
        var sid = _current.GetSessionId();
        if (string.IsNullOrWhiteSpace(sid)) return null;
        return await q.FirstOrDefaultAsync(c => c.SessionId == sid, ct);
    }
}
