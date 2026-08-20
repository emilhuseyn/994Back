using ECommerce.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECommerce.API.Middlewares;

public class FluentValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _provider;

    public FluentValidationFilter(IServiceProvider provider)
    {
        _provider = provider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (_provider.GetService(validatorType) is IValidator validator)
            {
                var contextObj = new ValidationContext<object>(arg);
                var result = await validator.ValidateAsync(contextObj, context.HttpContext.RequestAborted);
                if (!result.IsValid)
                {
                    var errors = result.Errors.Select(e => e.ErrorMessage);
                    context.Result = new UnprocessableEntityObjectResult(
                        ApiResponse.Fail("Validasiya xətası.", errors));
                    return;
                }
            }
        }
        await next();
    }
}
