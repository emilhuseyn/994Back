namespace ECommerce.Application.DTOs;

/// <summary>Generic paged envelope returned by the 1C read (pull) endpoints.</summary>
public class SyncPagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new();
}

/// <summary>An order, shaped for the 1C pull endpoint.</summary>
public class SyncOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    /// <summary>Enum names (e.g. "Pending", "Shipped") so 1C reads them directly.</summary>
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int? UserId { get; set; }
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<SyncOrderItemDto> Items { get; set; } = new();
}

public class SyncOrderItemDto
{
    /// <summary>The product's 1C uid (ExternalId) — null if the product was created manually in admin.</summary>
    public string? ProductExternalId { get; set; }
    /// <summary>
    /// The variant's 1C uid — exactly the "ItemuUid" 1C sent on sync. This is
    /// the line identifier to match an order row back to 1C. Null for variants
    /// created manually in admin.
    /// </summary>
    public string? ItemExternalId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSku { get; set; }
    /// <summary>Variant SKU — the "Item" label 1C originally sent for this variant.</summary>
    public string? VariantSku { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

/// <summary>A user/customer, shaped for the 1C pull endpoint (no secrets).</summary>
public class SyncUserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    /// <summary>"Customer" or "Admin".</summary>
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public int OrderCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
