using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Application.Services.Abstractions;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default);
    Task ResendCodeAsync(ResendCodeRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<UserDto> GetMeAsync(CancellationToken ct = default);
}

public interface IProductService
{
    Task<PaginatedList<ProductListItemDto>> ListAsync(ProductQueryParameters parameters, CancellationToken ct = default);
    Task<ProductDetailDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<ProductDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDetailDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductDetailDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<List<ProductImageDto>> AddImagesAsync(int productId, IEnumerable<IFormFile> files, CancellationToken ct = default);
    Task DeleteImageAsync(int imageId, CancellationToken ct = default);
    Task<ProductImageDto> SetMainImageAsync(int imageId, CancellationToken ct = default);
    Task<ProductVariantDto> AddVariantAsync(int productId, AddVariantRequest request, CancellationToken ct = default);
    Task<ProductVariantDto> UpdateVariantAsync(int variantId, UpdateVariantRequest request, CancellationToken ct = default);
    Task DeleteVariantAsync(int variantId, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<List<CategoryTreeDto>> GetTreeAsync(CancellationToken ct = default);
    Task<CategoryDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IBrandService
{
    Task<List<BrandDto>> ListAsync(CancellationToken ct = default);
    Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken ct = default);
    Task<BrandDto> UpdateAsync(int id, UpdateBrandRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IFilterService
{
    Task<FiltersDto> GetAsync(CancellationToken ct = default);
}

public interface ICartService
{
    Task<CartDto> GetAsync(CancellationToken ct = default);
    Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken ct = default);
    Task<CartDto> UpdateItemAsync(int itemId, UpdateCartItemRequest request, CancellationToken ct = default);
    Task<CartDto> RemoveItemAsync(int itemId, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<List<OrderDto>> ListMineAsync(CancellationToken ct = default);
    Task<PaginatedList<OrderDto>> ListAllAsync(OrderQueryParameters parameters, CancellationToken ct = default);
    Task<OrderDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<OrderDto> UpdateStatusAsync(int id, UpdateOrderStatusRequest request, CancellationToken ct = default);
}

public interface IWishlistService
{
    Task<List<WishlistItemDto>> ListAsync(CancellationToken ct = default);
    Task AddAsync(int productId, CancellationToken ct = default);
    Task RemoveAsync(int productId, CancellationToken ct = default);
}

public interface IContactService
{
    Task SubmitAsync(CreateContactMessageRequest request, CancellationToken ct = default);
    Task<PaginatedList<ContactMessageDto>> ListAsync(int page, int pageSize, bool? isRead = null, CancellationToken ct = default);
    Task MarkReadAsync(int id, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default);
}

public interface IThemeService
{
    Task<ThemeDto> GetAsync(CancellationToken ct = default);
    Task<ThemeDto> UpdateAsync(ThemeDto theme, CancellationToken ct = default);
    Task<ThemeDto> ResetAsync(CancellationToken ct = default);
}

public interface IUserAdminService
{
    Task<PaginatedList<UserDto>> ListAsync(int page, int pageSize, string? search = null, int? role = null, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task<UserDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken ct = default);
    Task DeactivateAsync(int id, CancellationToken ct = default);
}

public interface ISliderService
{
    Task<List<SliderDto>> ListAsync(bool onlyActive, CancellationToken ct = default);
    Task<SliderDto> CreateAsync(CreateSliderRequest request, CancellationToken ct = default);
    Task<SliderDto> UpdateAsync(int id, UpdateSliderRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ISiteSettingService
{
    Task<List<SiteSettingDto>> ListAsync(CancellationToken ct = default);
    Task<SiteSettingDto> UpdateAsync(string key, UpdateSiteSettingRequest request, CancellationToken ct = default);
}

public interface IStylistService
{
    /// <summary>
    /// Build a 4-card outfit (top + bottom + shoes + accessory) anchored on
    /// the given product.  Returns a graceful empty result when Gemini is
    /// not configured or the catalog is too small to fill all 4 slots.
    /// </summary>
    Task<StylistSuggestionDto> SuggestAsync(StylistRequestDto request, CancellationToken ct = default);
}

public interface IInventorySyncService
{
    /// <summary>
    /// Upsert the catalogue from a 1C export: products matched by external uid
    /// are updated, new ones created, and referenced brands/categories/colours/
    /// sizes are created on demand. Runs in a single transaction.
    /// </summary>
    Task<InventorySyncResultDto> SyncAsync(InventorySyncRequest request, CancellationToken ct = default);

    /// <summary>Paged orders for the 1C pull endpoint; <paramref name="since"/> filters by created/updated date.</summary>
    Task<SyncPagedResult<SyncOrderDto>> GetOrdersAsync(int page, int pageSize, DateTime? since, CancellationToken ct = default);

    /// <summary>Paged users/customers for the 1C pull endpoint (no secrets).</summary>
    Task<SyncPagedResult<SyncUserDto>> GetUsersAsync(int page, int pageSize, DateTime? since, CancellationToken ct = default);
}
