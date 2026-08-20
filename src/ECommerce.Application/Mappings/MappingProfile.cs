using AutoMapper;
using ECommerce.Application.DTOs;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>();

        CreateMap<Brand, BrandDto>()
            .ForMember(d => d.ProductCount, opt => opt.MapFrom(s => s.Products.Count));

        CreateMap<Category, CategoryDto>()
            .ForMember(d => d.ProductCount, opt => opt.MapFrom(s => s.Products.Count));

        CreateMap<Category, CategoryTreeDto>()
            .ForMember(d => d.ProductCount, opt => opt.MapFrom(s => s.Products.Count))
            .ForMember(d => d.Children, opt => opt.MapFrom(s => s.Children));

        CreateMap<Color, ColorDto>();
        CreateMap<Size, SizeDto>();

        CreateMap<ProductImage, ProductImageDto>();
        CreateMap<ProductVariant, ProductVariantDto>()
            .ForMember(d => d.ColorNameAz, opt => opt.MapFrom(s => s.Color.NameAz))
            .ForMember(d => d.ColorHex, opt => opt.MapFrom(s => s.Color.HexCode))
            .ForMember(d => d.SizeName, opt => opt.MapFrom(s => s.Size.Name));

        CreateMap<Product, ProductListItemDto>()
            .ForMember(d => d.EffectivePrice, opt => opt.MapFrom(s => s.EffectivePrice))
            .ForMember(d => d.BrandName, opt => opt.MapFrom(s => s.Brand.Name))
            .ForMember(d => d.BrandSlug, opt => opt.MapFrom(s => s.Brand.Slug))
            .ForMember(d => d.CategoryNameAz, opt => opt.MapFrom(s => s.Category.NameAz))
            .ForMember(d => d.CategoryNameRu, opt => opt.MapFrom(s => s.Category.NameRu))
            .ForMember(d => d.CategoryNameEn, opt => opt.MapFrom(s => s.Category.NameEn))
            .ForMember(d => d.CategorySlug, opt => opt.MapFrom(s => s.Category.Slug))
            .ForMember(d => d.MainImageUrl, opt => opt.MapFrom(s =>
                s.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault()))
            .ForMember(d => d.HoverImageUrl, opt => opt.MapFrom(s =>
                s.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder).Skip(1).Select(i => i.ImageUrl).FirstOrDefault()))
            .ForMember(d => d.Colors, opt => opt.MapFrom(s =>
                s.Variants.Where(v => v.IsActive)
                    .Select(v => v.Color.NameAz)
                    .Distinct()
                    .ToList()))
            .ForMember(d => d.Sizes, opt => opt.MapFrom(s =>
                s.Variants.Where(v => v.IsActive)
                    .Select(v => v.Size.Name)
                    .Distinct()
                    .ToList()));

        CreateMap<Product, ProductDetailDto>()
            .IncludeBase<Product, ProductListItemDto>()
            .ForMember(d => d.Images, opt => opt.MapFrom(s => s.Images.OrderBy(i => i.SortOrder)))
            .ForMember(d => d.Variants, opt => opt.MapFrom(s => s.Variants))
            .ForMember(d => d.TotalStock, opt => opt.MapFrom(s => s.Variants.Sum(v => v.StockQuantity)));

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Items, opt => opt.MapFrom(s => s.Items));
        CreateMap<OrderItem, OrderItemDto>();

        CreateMap<Wishlist, WishlistItemDto>()
            .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.NameAz))
            .ForMember(d => d.ProductSlug, opt => opt.MapFrom(s => s.Product.Slug))
            .ForMember(d => d.EffectivePrice, opt => opt.MapFrom(s => s.Product.EffectivePrice))
            .ForMember(d => d.MainImageUrl, opt => opt.MapFrom(s =>
                s.Product.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault()));

        CreateMap<ContactMessage, ContactMessageDto>();
        CreateMap<Slider, SliderDto>();
        CreateMap<SiteSetting, SiteSettingDto>();
    }
}
