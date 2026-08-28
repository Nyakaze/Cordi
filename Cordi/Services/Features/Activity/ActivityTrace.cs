namespace Cordi.Services.Activity;

public readonly struct ActivityTrace
{
    private const string Prefix = "[ActivityManager]";

    private readonly bool _verbose;

    private ActivityTrace(bool verbose) => _verbose = verbose;

    public static ActivityTrace Verbose => new(true);

    public static ActivityTrace Quiet => new(false);

    public void Debug(string message)
    {
        if (_verbose) Service.Log.Debug($"{Prefix} {message}");
    }

    public void Info(string message)
    {
        if (_verbose) Service.Log.Info($"{Prefix} {message}");
    }

    public void Warning(string message)
    {
        if (_verbose) Service.Log.Warning($"{Prefix} {message}");
    }
}
