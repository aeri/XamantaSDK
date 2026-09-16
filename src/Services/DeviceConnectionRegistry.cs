using System.Collections.Concurrent;

namespace XamantaSDK.Services;

public class DeviceConnectionRegistry
{
    public DeviceChannels<ApplyPolicyRequest> Policy { get; } = new();
    public DeviceChannels<Command> Commands { get; } = new();

    private readonly ConcurrentDictionary<string,
        (CommandType Type, TaskCompletionSource<CommandResult> Completion)> _pendingCommands = new();

    public bool IsConnected(string deviceId) => Policy.IsConnected(deviceId);

    public void Disconnect(string deviceId)
    {
        Policy.Remove(deviceId);
        Commands.Remove(deviceId);
    }

    public TaskCompletionSource<CommandResult> TrackCommand(string commandId, CommandType type)
    {
        var tcs = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCommands[commandId] = (type, tcs);
        return tcs;
    }

    public CommandType? CompleteCommand(CommandResult result)
    {
        if (!_pendingCommands.TryRemove(result.Id, out var pending))
            return null;

        pending.Completion.TrySetResult(result);
        return pending.Type;
    }
}
