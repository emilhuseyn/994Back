using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _current;

    public CartService(IUnitOfWork uow, ICurrentUserService current)
    {
        _uow = uow;
        _current = current;
    }

    private IQueryable<Cart> BaseCartQuery() =>
        _uow.Carts.Query()
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Images)
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Color)
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Size);

    private async Task<Cart> GetOrCreateCartAsync(CancellationToken ct)
    {
        if (_current.UserId is int uid)
        {
            var cart = await BaseCartQuery().FirstOrDefaultAsync(c => c.UserId == uid, ct);
            if (cart != null) return cart;
            cart = new Cart { UserId = uid };
            await _uow.Carts.AddAsync(cart, ct);
            await _uow.SaveChangesAsync(ct);
            return cart;
        }

        var sid = _current.GetSessionId();
        if (string.IsNullOrWhiteSpace(sid))
            throw new UnauthorizedException("Session-id başlığı tapılmadı.");

        var guest = await BaseCartQuery().FirstOrDefaultAsync(c => c.SessionId == sid, ct);
        if (guest != null) return guest;
        guest = new Cart { SessionId = sid };
        await _uow.Carts.AddAsync(guest, ct);
        await _uow.SaveChangesAsync(ct);
        return guest;
    }

    public async Task<CartDto> GetAsync(CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(ct);
        return ToDto(cart);
    }

    public async Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(ct);

        var variant = await _uow.ProductVariants.Query()
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId && v.IsActive, ct)
            ?? throw new NotFoundException("Variant");

        if (variant.StockQuantity < request.Quantity)
            throw new ConflictException("Kifayət qədər stok yoxdur.");

        var unitPrice = variant.Product.EffectivePrice + variant.PriceAdjustment;
        var existing = cart.Items.FirstOrDefault(i => i.ProductVariantId == variant.Id);
        if (existing is null)
        {
            var item = new CartItem
            {
                CartId = cart.Id,
                ProductVariantId = variant.Id,
                Quantity = request.Quantity,
                UnitPrice = unitPrice
            };
            cart.Items.Add(item);
            await _uow.CartItems.AddAsync(item, ct);
        }
        else
        {
            var newQty = existing.Quantity + request.Quantity;
            if (variant.StockQuantity < newQty)
                throw new ConflictException("Kifayət qədər stok yoxdur.");
            existing.Quantity = newQty;
            existing.UnitPrice = unitPrice;
            _uow.CartItems.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);

        var fresh = await BaseCartQuery().FirstAsync(c => c.Id == cart.Id, ct);
        return ToDto(fresh);
    }

    public async Task<CartDto> UpdateItemAsync(int itemId, UpdateCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(ct);
        var item = cart.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Səbət elementi");

        var variant = await _uow.ProductVariants.GetByIdAsync(item.ProductVariantId, ct);
        if (variant != null && variant.StockQuantity < request.Quantity)
            throw new ConflictException("Kifayət qədər stok yoxdur.");

        item.Quantity = request.Quantity;
        _uow.CartItems.Update(item);
        await _uow.SaveChangesAsync(ct);

        var fresh = await BaseCartQuery().FirstAsync(c => c.Id == cart.Id, ct);
        return ToDto(fresh);
    }

    public async Task<CartDto> RemoveItemAsync(int itemId, CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(ct);
        var item = cart.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Səbət elementi");

        _uow.CartItems.Remove(item);
        await _uow.SaveChangesAsync(ct);

        var fresh = await BaseCartQuery().FirstAsync(c => c.Id == cart.Id, ct);
        return ToDto(fresh);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var cart = await GetOrCreateCartAsync(ct);
        foreach (var item in cart.Items.ToList())
            _uow.CartItems.Remove(item);
        await _uow.SaveChangesAsync(ct);
    }

    private static CartDto ToDto(Cart cart) => new()
    {
        Id = cart.Id,
        UserId = cart.UserId,
        SessionId = cart.SessionId,
        Items = cart.Items.Select(i => new CartItemDto
        {
            Id = i.Id,
            ProductVariantId = i.ProductVariantId,
            ProductId = i.ProductVariant.ProductId,
            ProductName = i.ProductVariant.Product.NameAz,
            ProductSlug = i.ProductVariant.Product.Slug,
            ColorName = i.ProductVariant.Color.NameAz,
            SizeName = i.ProductVariant.Size.Name,
            ImageUrl = i.ProductVariant.Product.Images
                .OrderByDescending(img => img.IsMain).ThenBy(img => img.SortOrder)
                .Select(img => img.ImageUrl).FirstOrDefault(),
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            StockAvailable = i.ProductVariant.StockQuantity
        }).ToList()
    };
}
