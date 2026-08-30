using System;
using Dalamud.Plugin.Services;

namespace Cordi.Domain;

public class LocalPlayerProvider : IDisposable
{
    private bool _disposed;

    public LocalPlayer? Current { get; private set; }

    public event Action<LocalPlayer>? OnLogin;
    public event Action? OnLogout;

    public LocalPlayerProvider()
    {
        Service.ClientState.Logout += HandleLogout;
    }

    public void Tick(IFramework framework)
    {
        if (_disposed) return;

        var lp = Service.ObjectTable.LocalPlayer;

        if (lp == null)
        {
            if (Current != null)
            {
                Current = null;
                OnLogout?.Invoke();
            }
            return;
        }

        if (Current == null || Current.GameObjectId != lp.GameObjectId)
        {
            Current = LocalPlayer.From(lp);
            OnLogin?.Invoke(Current);
            return;
        }

        Current.Refresh(lp);
    }

    private void HandleLogout(int type, int code)
    {
        if (Current != null)
        {
            Current = null;
            OnLogout?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Service.ClientState.Logout -= HandleLogout;
        Current = null;
    }
}
