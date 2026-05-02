using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public class ValidationExceptionMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException vex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/problem+json";
            var details = new ValidationProblemDetails(
                vex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            };
            await ctx.Response.WriteAsJsonAsync(details);
        }
    }
}
