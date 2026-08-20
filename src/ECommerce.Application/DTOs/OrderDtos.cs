using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs;

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class CreateOrderRequest
{
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
    public List<CreateOrderItemRequest>? Items { get; set; }
}

public record CreateOrderItemRequest(int ProductVariantId, int Quantity);

public record UpdateOrderStatusRequest(OrderStatus Status, PaymentStatus? PaymentStatus);

/// <summary>
/// Admin order-list query: filtering + sorting + pagination.  All fields are
/// optional; an unset field simply doesn't constrain the result.
/// </summary>
public class OrderQueryParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Filter by order status (Pending, Shipped, …).</summary>
    public OrderStatus? Status { get; set; }
    /// <summary>Filter by payment status (Pending, Paid, …).</summary>
    public PaymentStatus? PaymentStatus { get; set; }
    /// <summary>Filter by payment method (Cash, Card, Online).</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Free-text search across order number, name, email, phone.</summary>
    public string? Search { get; set; }

    /// <summary>Inclusive lower bound on <c>CreatedAt</c> (UTC date).</summary>
    public DateTime? DateFrom { get; set; }
    /// <summary>Inclusive upper bound on <c>CreatedAt</c> (whole day included).</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Inclusive lower bound on <c>TotalAmount</c>.</summary>
    public decimal? MinTotal { get; set; }
    /// <summary>Inclusive upper bound on <c>TotalAmount</c>.</summary>
    public decimal? MaxTotal { get; set; }

    /// <summary>
    /// Sort key: <c>newest</c> (default), <c>oldest</c>, <c>total_desc</c>,
    /// <c>total_asc</c>, <c>status</c>.
    /// </summary>
    public string? Sort { get; set; }
}
