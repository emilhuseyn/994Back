using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Email).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.RefreshToken).HasMaxLength(200);
        b.Property(x => x.Role).HasConversion<int>();
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(180);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.HasIndex(x => x.ExternalId);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAz).IsRequired().HasMaxLength(150);
        b.Property(x => x.NameRu).IsRequired().HasMaxLength(150);
        b.Property(x => x.NameEn).HasMaxLength(150);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(180);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.HasIndex(x => x.ExternalId);
        b.HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ColorConfiguration : IEntityTypeConfiguration<Color>
{
    public void Configure(EntityTypeBuilder<Color> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAz).IsRequired().HasMaxLength(80);
        b.Property(x => x.NameRu).IsRequired().HasMaxLength(80);
        b.Property(x => x.NameEn).HasMaxLength(80);
        b.Property(x => x.HexCode).IsRequired().HasMaxLength(20);
        b.HasIndex(x => x.NameAz).IsUnique();
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.HasIndex(x => x.ExternalId);
    }
}

public class SizeConfiguration : IEntityTypeConfiguration<Size>
{
    public void Configure(EntityTypeBuilder<Size> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(20);
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.HasIndex(x => x.ExternalId);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAz).IsRequired().HasMaxLength(250);
        b.Property(x => x.NameRu).IsRequired().HasMaxLength(250);
        b.Property(x => x.NameEn).HasMaxLength(250);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(280);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.SKU).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.SKU).IsUnique();
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.HasIndex(x => x.ExternalId);
        b.Property(x => x.BasePrice).HasColumnType("decimal(18,2)");
        b.Property(x => x.DiscountPrice).HasColumnType("decimal(18,2)");
        b.Property(x => x.Gender).HasConversion<int>();
        b.Property(x => x.DescriptionAz).HasMaxLength(4000);
        b.Property(x => x.DescriptionRu).HasMaxLength(4000);
        b.Property(x => x.DescriptionEn).HasMaxLength(4000);

        b.HasOne(x => x.Brand)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.EffectivePrice);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        b.Property(x => x.AltText).HasMaxLength(250);
        b.HasOne(x => x.Product)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PriceAdjustment).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        b.Property(x => x.SKU).IsRequired().HasMaxLength(120);
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.HasIndex(x => x.ExternalId);
        b.HasIndex(x => new { x.ProductId, x.ColorId, x.SizeId }).IsUnique();
        b.HasOne(x => x.Product)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Color)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.ColorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Size)
            .WithMany(x => x.Variants)
            .HasForeignKey(x => x.SizeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.SessionId).HasMaxLength(100);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.SessionId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
        b.HasOne(x => x.Cart)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ProductVariant)
            .WithMany(x => x.CartItems)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.OrderNumber).IsRequired().HasMaxLength(40);
        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.Property(x => x.CustomerFullName).IsRequired().HasMaxLength(150);
        b.Property(x => x.CustomerEmail).IsRequired().HasMaxLength(200);
        b.Property(x => x.CustomerPhone).IsRequired().HasMaxLength(30);
        b.Property(x => x.DeliveryAddress).IsRequired().HasMaxLength(500);
        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.PaymentStatus).HasConversion<int>();
        b.Property(x => x.PaymentMethod).HasConversion<int>();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.User).WithMany(u => u.Orders).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductName).IsRequired().HasMaxLength(250);
        b.Property(x => x.ColorName).HasMaxLength(80);
        b.Property(x => x.SizeName).HasMaxLength(20);
        b.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalPrice).HasColumnType("decimal(18,2)");
        b.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ProductVariant).WithMany(x => x.OrderItems).HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WishlistConfiguration : IEntityTypeConfiguration<Wishlist>
{
    public void Configure(EntityTypeBuilder<Wishlist> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        b.HasOne(x => x.User).WithMany(u => u.Wishlists).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product).WithMany(p => p.Wishlists).HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Email).IsRequired().HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Message).IsRequired().HasMaxLength(2000);
    }
}

public class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.ValueAz).HasMaxLength(2000);
        b.Property(x => x.ValueRu).HasMaxLength(2000);
        b.Property(x => x.ValueEn).HasMaxLength(2000);
    }
}

public class SliderConfiguration : IEntityTypeConfiguration<Slider>
{
    public void Configure(EntityTypeBuilder<Slider> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TitleAz).IsRequired().HasMaxLength(200);
        b.Property(x => x.TitleRu).IsRequired().HasMaxLength(200);
        b.Property(x => x.TitleEn).HasMaxLength(200);
        b.Property(x => x.SubtitleAz).HasMaxLength(400);
        b.Property(x => x.SubtitleRu).HasMaxLength(400);
        b.Property(x => x.SubtitleEn).HasMaxLength(400);
        b.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        b.Property(x => x.ButtonTextAz).HasMaxLength(100);
        b.Property(x => x.ButtonTextRu).HasMaxLength(100);
        b.Property(x => x.ButtonTextEn).HasMaxLength(100);
        b.Property(x => x.ButtonUrl).HasMaxLength(500);
    }
}
