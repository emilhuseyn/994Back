using System.Reflection;
using ECommerce.Application.Services;
using ECommerce.Application.Services.Abstractions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IFilterService, FilterService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IWishlistService, WishlistService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ISliderService, SliderService>();
        services.AddScoped<ISiteSettingService, SiteSettingService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<IStylistService, StylistService>();
        services.AddScoped<IInventorySyncService, InventorySyncService>();

        return services;
    }
}
