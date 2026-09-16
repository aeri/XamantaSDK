using Microsoft.AspNetCore.Mvc.Filters;

namespace XamantaSDK.Infrastructure;

public sealed class NoStoreAttribute : ActionFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers.Pragma = "no-cache";
        base.OnResultExecuting(context);
    }
}
