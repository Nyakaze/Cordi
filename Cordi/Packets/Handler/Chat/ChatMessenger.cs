using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using ECommons.Automation;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace Cordi.Packets.Handler.Chat;

public class ChatMessenger : IAsyncDisposable
{

    private readonly IChatGui _chat;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly ICommandManager _cmd;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TimeSpan MinInterval { get; init; } = TimeSpan.FromSeconds(1.0);
    public int MaxLength { get; init; } = 450;

    private DateTime _lastSendUtc = DateTime.MinValue;
    private readonly MethodInfo? _sendMessageMI;

    public ChatMessenger(IChatGui chatGui, IFramework framework, IClientState clientState, ICommandManager cmd)
    {
        _chat = chatGui;
        _framework = framework;
        _clientState = clientState;
        _cmd = cmd;

        _sendMessageMI = _chat.GetType().GetMethod("SendMessage", new[] { typeof(string) });
    }





    public async Task SendAsync(XivChatType type, string message, string? tellTarget = null, bool echoLocally = false, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_clientState.IsLoggedIn)
            {
                _chat.PrintError("Nicht eingeloggt – Nachricht nicht gesendet.", "Cordi");
                return;
            }

            var cmd = type switch
            {
                XivChatType.Say => "/say",
                XivChatType.Shout => "/sh",
                XivChatType.Yell => "/y",
                XivChatType.Party => "/p",
                XivChatType.Alliance => "/a",
                XivChatType.FreeCompany => "/fc",
                XivChatType.Ls1 => "/ls1",
                XivChatType.Ls2 => "/ls2",
                XivChatType.Ls3 => "/ls3",
                XivChatType.Ls4 => "/ls4",
                XivChatType.Ls5 => "/ls5",
                XivChatType.Ls6 => "/ls6",
                XivChatType.Ls7 => "/ls7",
                XivChatType.Ls8 => "/ls8",
                XivChatType.CrossLinkShell1 => "/cwl1",
                XivChatType.CrossLinkShell2 => "/cwl2",
                XivChatType.CrossLinkShell3 => "/cwl3",
                XivChatType.CrossLinkShell4 => "/cwl4",
                XivChatType.CrossLinkShell5 => "/cwl5",
                XivChatType.CrossLinkShell6 => "/cwl6",
                XivChatType.CrossLinkShell7 => "/cwl7",
                XivChatType.CrossLinkShell8 => "/cwl8",
                XivChatType.TellOutgoing => "/tell",
                _ => null
            };

            if (cmd is null)
            {
                _chat.PrintError($"{type} ist kein sendbarer Kanal.", "Cordi");
                return;
            }

            var msg = Sanitize(message, MaxLength);
            string full = (type == XivChatType.TellOutgoing)
                ? $"{cmd} {tellTarget} {msg}"
                : $"{cmd} {msg}";


            var now = DateTime.UtcNow;
            var wait = _lastSendUtc + MinInterval - now;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct).ConfigureAwait(false);


            var tcs = new TaskCompletionSource();
            await _framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    ECommons.Automation.Chat.SendMessage(full);
                    tcs.SetResult();
                }
                catch (Exception ex)
                {

                    _chat.PrintError($"[Chat] Send failed: {ex.Message}");
                    tcs.SetException(ex);
                }
            });
            await tcs.Task.ConfigureAwait(false);

            _lastSendUtc = DateTime.UtcNow;
            if (echoLocally) _chat.Print($"[→ {type}] {msg}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SendTellAsync(string targetNameWorld, string message, bool echoLocally = false, CancellationToken ct = default)
        => SendAsync(XivChatType.TellOutgoing, message, targetNameWorld, echoLocally, ct);

    public void SendMessage(string message)
    {
        ECommons.Automation.Chat.SendMessage(message);
    }

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }


    private static string Sanitize(string s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (s.Length <= max) return s;
        const string ell = "…";
        return s[..Math.Max(0, max - ell.Length)] + ell;
    }
    private static string QuoteNameIfNeeded(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return "\"\"";
        return target.Any(char.IsWhiteSpace) ? $"\"{target}\"" : target;
    }
}
