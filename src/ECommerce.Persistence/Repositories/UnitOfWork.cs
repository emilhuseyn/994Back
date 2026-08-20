using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Context;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _ctx;
    private IDbContextTransaction? _tx;

    public UnitOfWork(ApplicationDbContext ctx)
    {
        _ctx = ctx;

        Users = new Repository<User>(ctx);
        Brands = new Repository<Brand>(ctx);
        Categories = new Repository<Category>(ctx);
        Colors = new Repository<Color>(ctx);
        Sizes = new Repository<Size>(ctx);
        Products = new Repository<Product>(ctx);
        ProductImages = new Repository<ProductImage>(ctx);
        ProductVariants = new Repository<ProductVariant>(ctx);
        Carts = new Repository<Cart>(ctx);
        CartItems = new Repository<CartItem>(ctx);
        Orders = new Repository<Order>(ctx);
        OrderItems = new Repository<OrderItem>(ctx);
        Wishlists = new Repository<Wishlist>(ctx);
        ContactMessages = new Repository<ContactMessage>(ctx);
        SiteSettings = new Repository<SiteSetting>(ctx);
        Sliders = new Repository<Slider>(ctx);
    }

    public IRepository<User> Users { get; }
    public IRepository<Brand> Brands { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<Color> Colors { get; }
    public IRepository<Size> Sizes { get; }
    public IRepository<Product> Products { get; }
    public IRepository<ProductImage> ProductImages { get; }
    public IRepository<ProductVariant> ProductVariants { get; }
    public IRepository<Cart> Carts { get; }
    public IRepository<CartItem> CartItems { get; }
    public IRepository<Order> Orders { get; }
    public IRepository<OrderItem> OrderItems { get; }
    public IRepository<Wishlist> Wishlists { get; }
    public IRepository<ContactMessage> ContactMessages { get; }
    public IRepository<SiteSetting> SiteSettings { get; }
    public IRepository<Slider> Sliders { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_tx != null) return;
        _tx = await _ctx.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is null) return;
        await _tx.CommitAsync(ct);
        await _tx.DisposeAsync();
        _tx = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is null) return;
        await _tx.RollbackAsync(ct);
        await _tx.DisposeAsync();
        _tx = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_tx != null) await _tx.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
