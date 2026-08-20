using ECommerce.Application.DTOs;
using FluentValidation;

namespace ECommerce.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(100);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.NameAz).NotEmpty().MaximumLength(250);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(250);
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountPrice)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.BasePrice)
            .When(x => x.DiscountPrice.HasValue)
            .WithMessage("Endirim qiyməti baza qiymətindən böyük ola bilməz.");
        RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleForEach(x => x.Variants).SetValidator(new CreateVariantRequestValidator());
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        Include(new CreateProductRequestValidator());
    }
}

public class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
{
    public CreateVariantRequestValidator()
    {
        RuleFor(x => x.ColorId).GreaterThan(0);
        RuleFor(x => x.SizeId).GreaterThan(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
    }
}

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.NameAz).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(150);
    }
}

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        Include(new CreateCategoryRequestValidator());
    }
}

public class CreateBrandRequestValidator : AbstractValidator<CreateBrandRequest>
{
    public CreateBrandRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class UpdateBrandRequestValidator : AbstractValidator<UpdateBrandRequest>
{
    public UpdateBrandRequestValidator()
    {
        Include(new CreateBrandRequestValidator());
    }
}

public class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductVariantId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerFullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.CustomerPhone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DeliveryAddress).NotEmpty().MaximumLength(500);
        When(x => x.Items != null && x.Items.Count > 0, () =>
        {
            RuleForEach(x => x.Items!).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductVariantId).GreaterThan(0);
                item.RuleFor(i => i.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
            });
        });
    }
}

public class CreateContactMessageValidator : AbstractValidator<CreateContactMessageRequest>
{
    public CreateContactMessageValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
    }
}
