using Application.Rewards.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class RewardErrorHandler
{
    public static ObjectResult ToObjectResult(this RewardException ex) => new(ex.Message)
    {
        StatusCode = ex switch
        {
            RewardNotFoundException => StatusCodes.Status404NotFound,
            RewardOutOfStockException
                or RewardInactiveException => StatusCodes.Status422UnprocessableEntity,
            RewardUnknownException => StatusCodes.Status500InternalServerError,
            _ => throw new NotImplementedException($"Unmapped reward exception: {ex.GetType().Name}")
        }
    };
}
