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

public class WishlistService : IWishlistService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _current;

    public WishlistService(IUnitOfWork uow, IMapper mapper, ICurrentUserService current)
    {
        _uow = uow; _mapper = mapper; _current = current;
    }

    private int RequireUserId() => _current.UserId ?? throw new UnauthorizedException();

    public async Task<List<WishlistItemDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = RequireUserId();
        var items = await _uow.Wishlists.Query()
            .Include(w => w.Product).ThenInclude(p => p.Images)
            .Where(w => w.UserId == uid)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
        return _mapper.Map<List<WishlistItemDto>>(items);
    }

    public async Task AddAsync(int productId, CancellationToken ct = default)
    {
        var uid = RequireUserId();
        if (!await _uow.Products.AnyAsync(p => p.Id == productId, ct))
            throw new NotFoundException("Məhsul");
        if (await _uow.Wishlists.AnyAsync(w => w.UserId == uid && w.ProductId == productId, ct))
            return;
        await _uow.Wishlists.AddAsync(new Wishlist { UserId = uid, ProductId = productId }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(int productId, CancellationToken ct = default)
    {
        var uid = RequireUserId();
        var item = await _uow.Wishlists.FirstOrDefaultAsync(w => w.UserId == uid && w.ProductId == productId, ct);
        if (item is null) return;
        _uow.Wishlists.Remove(item);
        await _uow.SaveChangesAsync(ct);
    }
}

public class ContactService : IContactService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IEmailService _email;

    public ContactService(IUnitOfWork uow, IMapper mapper, IEmailService email)
    {
        _uow = uow; _mapper = mapper; _email = email;
    }

    public async Task SubmitAsync(CreateContactMessageRequest request, CancellationToken ct = default)
    {
        var msg = new ContactMessage
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Message = request.Message.Trim()
        };
        await _uow.ContactMessages.AddAsync(msg, ct);
        await _uow.SaveChangesAsync(ct);

        // Notify the store mailbox so the admin sees new messages without
        // having to poll the panel.  Fire-and-forget; the customer's
        // submission succeeds regardless of email delivery.
        var adminEmail = _email.AdminEmail;
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var html = EmailTemplates.ContactNotification(msg);
            _ = _email.SendAsync(adminEmail, "Code994", $"Yeni əlaqə mesajı · {msg.FullName}", html);
        }
    }

    public async Task<PaginatedList<ContactMessageDto>> ListAsync(int page, int pageSize, bool? isRead = null, CancellationToken ct = default)
    {
        var q = _uow.ContactMessages.Query().AsQueryable();
        if (isRead.HasValue)
            q = q.Where(m => m.IsRead == isRead.Value);
        q = q.OrderByDescending(m => m.CreatedAt);

        var total = await q.CountAsync(ct);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        var items = await q.Skip((safePage - 1) * safeSize).Take(safeSize).ToListAsync(ct);
        return new PaginatedList<ContactMessageDto>(_mapper.Map<List<ContactMessageDto>>(items), total, safePage, safeSize);
    }

    public async Task MarkReadAsync(int id, CancellationToken ct = default)
    {
        var msg = await _uow.ContactMessages.GetByIdAsync(id, ct) ?? throw new NotFoundException("Mesaj");
        msg.IsRead = true;
        msg.UpdatedAt = DateTime.UtcNow;
        _uow.ContactMessages.Update(msg);
        await _uow.SaveChangesAsync(ct);
    }
}

public class SliderService : ISliderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public SliderService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow; _mapper = mapper;
    }

    public async Task<List<SliderDto>> ListAsync(bool onlyActive, CancellationToken ct = default)
    {
        var q = _uow.Sliders.Query();
        if (onlyActive) q = q.Where(s => s.IsActive);
        var items = await q.OrderBy(s => s.SortOrder).ToListAsync(ct);
        return _mapper.Map<List<SliderDto>>(items);
    }

    public async Task<SliderDto> CreateAsync(CreateSliderRequest r, CancellationToken ct = default)
    {
        var slider = new Slider
        {
            TitleAz = r.TitleAz, TitleRu = r.TitleRu, TitleEn = r.TitleEn,
            SubtitleAz = r.SubtitleAz, SubtitleRu = r.SubtitleRu, SubtitleEn = r.SubtitleEn,
            ImageUrl = r.ImageUrl,
            ButtonTextAz = r.ButtonTextAz, ButtonTextRu = r.ButtonTextRu, ButtonTextEn = r.ButtonTextEn,
            ButtonUrl = r.ButtonUrl,
            SortOrder = r.SortOrder, IsActive = r.IsActive
        };
        await _uow.Sliders.AddAsync(slider, ct);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<SliderDto>(slider);
    }

    public async Task<SliderDto> UpdateAsync(int id, UpdateSliderRequest r, CancellationToken ct = default)
    {
        var slider = await _uow.Sliders.GetByIdAsync(id, ct) ?? throw new NotFoundException("Slider");
        slider.TitleAz = r.TitleAz; slider.TitleRu = r.TitleRu; slider.TitleEn = r.TitleEn;
        slider.SubtitleAz = r.SubtitleAz; slider.SubtitleRu = r.SubtitleRu; slider.SubtitleEn = r.SubtitleEn;
        slider.ImageUrl = r.ImageUrl;
        slider.ButtonTextAz = r.ButtonTextAz; slider.ButtonTextRu = r.ButtonTextRu; slider.ButtonTextEn = r.ButtonTextEn;
        slider.ButtonUrl = r.ButtonUrl;
        slider.SortOrder = r.SortOrder; slider.IsActive = r.IsActive;
        slider.UpdatedAt = DateTime.UtcNow;
        _uow.Sliders.Update(slider);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<SliderDto>(slider);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var slider = await _uow.Sliders.GetByIdAsync(id, ct) ?? throw new NotFoundException("Slider");
        _uow.Sliders.Remove(slider);
        await _uow.SaveChangesAsync(ct);
    }
}

public class SiteSettingService : ISiteSettingService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public SiteSettingService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow; _mapper = mapper;
    }

    public async Task<List<SiteSettingDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _uow.SiteSettings.Query().OrderBy(s => s.Key).ToListAsync(ct);
        return _mapper.Map<List<SiteSettingDto>>(items);
    }

    public async Task<SiteSettingDto> UpdateAsync(string key, UpdateSiteSettingRequest request, CancellationToken ct = default)
    {
        var setting = await _uow.SiteSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            setting = new SiteSetting
            {
                Key = key,
                ValueAz = request.ValueAz,
                ValueRu = request.ValueRu,
                ValueEn = request.ValueEn
            };
            await _uow.SiteSettings.AddAsync(setting, ct);
        }
        else
        {
            setting.ValueAz = request.ValueAz;
            setting.ValueRu = request.ValueRu;
            setting.ValueEn = request.ValueEn;
            setting.UpdatedAt = DateTime.UtcNow;
            _uow.SiteSettings.Update(setting);
        }
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<SiteSettingDto>(setting);
    }
}

public class FilterService : IFilterService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public FilterService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow; _mapper = mapper;
    }

    public async Task<FiltersDto> GetAsync(CancellationToken ct = default)
    {
        var categories = await _uow.Categories.Query()
            .Include(c => c.Products)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NameAz).ToListAsync(ct);
        var brands = await _uow.Brands.Query().Include(b => b.Products)
            .OrderBy(b => b.Name).ToListAsync(ct);
        var colors = await _uow.Colors.Query().OrderBy(c => c.NameAz).ToListAsync(ct);
        var sizes = await _uow.Sizes.Query().OrderBy(s => s.SortOrder).ToListAsync(ct);

        var priceQuery = _uow.Products.Query().Where(p => p.IsActive);
        var min = await priceQuery.Select(p => (decimal?)(p.DiscountPrice ?? p.BasePrice)).MinAsync(ct) ?? 0m;
        var max = await priceQuery.Select(p => (decimal?)(p.DiscountPrice ?? p.BasePrice)).MaxAsync(ct) ?? 0m;

        return new FiltersDto
        {
            Categories = _mapper.Map<List<CategoryDto>>(categories),
            Brands = _mapper.Map<List<BrandDto>>(brands),
            Colors = _mapper.Map<List<ColorDto>>(colors),
            Sizes = _mapper.Map<List<SizeDto>>(sizes),
            Genders = new List<GenderDto>
            {
                new() { Value = Gender.Men, NameAz = "Kişilər üçün", NameRu = "Для мужчин" },
                new() { Value = Gender.Women, NameAz = "Qadınlar üçün", NameRu = "Для женщин" },
                new() { Value = Gender.Unisex, NameAz = "Uniseks", NameRu = "Унисекс" }
            },
            MinPrice = min,
            MaxPrice = max
        };
    }
}
