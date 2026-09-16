using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using XamantaSDK.Auth.Models;

namespace XamantaSDK.Infrastructure;

public sealed class OAuthErrorsAttribute(string error, string mediaType) : Attribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ProblemDetails } result)
            return;

        var status = result.StatusCode ?? StatusCodes.Status400BadRequest;
        var description = status == StatusCodes.Status415UnsupportedMediaType
            ? $"The request body must be {mediaType}"
            : $"The request body could not be read as {mediaType}";

        context.Result = new ObjectResult(new TokenError(error, description))
        {
            StatusCode = status
        };
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
