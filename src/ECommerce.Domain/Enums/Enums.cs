namespace ECommerce.Domain.Enums;

public enum UserRole
{
    Customer = 0,
    Admin = 1
}

public enum Gender
{
    Men = 0,
    Women = 1,
    Unisex = 2
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Preparing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Online = 2
}
