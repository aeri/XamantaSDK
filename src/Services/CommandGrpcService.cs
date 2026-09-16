using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using XamantaSDK.Data.Stores;

namespace XamantaSDK.Services;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CommandGrpcService(
    ILogger<CommandGrpcService> logger,
    DeviceConnectionRegistry registry,
    DeviceStore devices,
    DeviceEraser eraser) : CommandService.CommandServiceBase
{
    public override async Task CommandChannel(
        IAsyncStreamReader<CommandResult> requestStream,
        IServerStreamWriter<Command> responseStream,
        ServerCallContext context)
    {
        var deviceId = context.DeviceId();

        var connectedAt = DateTimeOffset.UtcNow;
        var channel = registry.Commands.Register(deviceId);
        logger.LogInformation("Device {DeviceId} connected to command channel", deviceId);
        await devices.TouchContactAsync(deviceId);

        var readTask = Task.Run(async () =>
        {
            await foreach (var result in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (result.Status == CommandStatus.Failed)
                    logger.LogWarning(
                        "Command {CommandId} from device {DeviceId} FAILED: reason={Reason}, message={Message}",
                        result.Id, deviceId, result.FailureReason, result.FailureMessage);
                else
                    logger.LogInformation(
                        "Command result from device {DeviceId}: id={CommandId}, status={Status}",
                        deviceId, result.Id, result.Status);
                var type = registry.CompleteCommand(result);

                if (result.Status == CommandStatus.Success && type is CommandType.Wipe or CommandType.Decommission)
                {
                    await eraser.EraseAsync(deviceId, $"{type} succeeded");
                    break;
                }

                await devices.TouchContactAsync(deviceId);
            }
        }, context.CancellationToken);

        var writeTask = Task.Run(async () =>
        {
            await foreach (var cmd in channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(cmd, context.CancellationToken);
                logger.LogInformation("Delivered command {CommandId} ({Type}) to device {DeviceId}",
                    cmd.Id, cmd.Type, deviceId);
            }
        }, context.CancellationToken);

        try
        {
            await Task.WhenAny(readTask, writeTask);
        }
        catch (OperationCanceledException) { }
        finally
        {
            registry.Commands.Unregister(deviceId, channel);
            logger.LogInformation("Device {DeviceId} disconnected from command channel after {DurationSeconds:F0}s",
                deviceId, (DateTimeOffset.UtcNow - connectedAt).TotalSeconds);
        }
    }
}
