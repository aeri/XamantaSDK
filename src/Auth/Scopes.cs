namespace XamantaSDK.Auth;

public static class Scopes
{
    public static HashSet<string> Split(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(scope.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    public static bool TryResolve(string? allowed, string? requested, out string? granted, out string disallowed)
    {
        disallowed = string.Empty;

        if (string.IsNullOrWhiteSpace(requested))
        {
            granted = allowed;
            return true;
        }

        var allowedSet = Split(allowed);
        var requestedSet = Split(requested);

        var extra = requestedSet.Where(s => !allowedSet.Contains(s)).ToList();
        if (extra.Count > 0)
        {
            granted = null;
            disallowed = string.Join(' ', extra);
            return false;
        }

        granted = string.Join(' ', requestedSet);
        return true;
    }
}
