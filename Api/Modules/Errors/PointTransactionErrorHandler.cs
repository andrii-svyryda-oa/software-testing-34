using Application.PointTransactions.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class PointTransactionErrorHandler
{
    public static ObjectResult ToObjectResult(this PointTransactionException ex) => new(ex.Message)
    {
        StatusCode = ex switch
        {
            PointTransactionUnknownException => StatusCodes.Status500InternalServerError,
            _ => throw new NotImplementedException($"Unmapped point transaction exception: {ex.GetType().Name}")
        }
    };
}
