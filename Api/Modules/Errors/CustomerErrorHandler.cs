using Application.Customers.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class CustomerErrorHandler
{
    public static ObjectResult ToObjectResult(this CustomerException ex) => new(ex.Message)
    {
        StatusCode = ex switch
        {
            CustomerNotFoundException => StatusCodes.Status404NotFound,
            CustomerAlreadyExistsException => StatusCodes.Status409Conflict,
            InsufficientPointsException
                or RedeemRewardOutOfStockException
                or RedeemRewardInactiveException => StatusCodes.Status422UnprocessableEntity,
            RedeemRewardNotFoundException => StatusCodes.Status404NotFound,
            CustomerUnknownException => StatusCodes.Status500InternalServerError,
            _ => throw new NotImplementedException($"Unmapped customer exception: {ex.GetType().Name}")
        }
    };
}
