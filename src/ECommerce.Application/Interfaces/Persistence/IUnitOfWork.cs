using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces.Persistence;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<User> Users { get; }
    IRepository<Brand> Brands { get; }
    IRepository<Category> Categories { get; }
    IRepository<Color> Colors { get; }
    IRepository<Size> Sizes { get; }
    IRepository<Product> Products { get; }
    IRepository<ProductImage> ProductImages { get; }
    IRepository<ProductVariant> ProductVariants { get; }
    IRepository<Cart> Carts { get; }
    IRepository<CartItem> CartItems { get; }
    IRepository<Order> Orders { get; }
    IRepository<OrderItem> OrderItems { get; }
    IRepository<Wishlist> Wishlists { get; }
    IRepository<ContactMessage> ContactMessages { get; }
    IRepository<SiteSetting> SiteSettings { get; }
    IRepository<Slider> Sliders { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
