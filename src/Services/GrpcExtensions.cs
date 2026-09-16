using System.Security.Claims;
using Grpc.Core;

namespace XamantaSDK.Services;

public static class GrpcExtensions
{
    public static string DeviceId(this ServerCallContext context) =>
        context.GetHttpContext().User.FindFirstValue("sub")
            ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing subject claim"));
}
