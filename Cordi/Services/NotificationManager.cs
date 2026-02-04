using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.UI.Windows;
using Dalamud.Interface;
using Cordi.UI.Themes;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using Cordi.Core;
using Cordi.Configuration;

namespace Cordi.Services;

public enum CordiNotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public struct CordiNotification
{
    public string Title;
    public string Message;
    public CordiNotificationType Type;
    public DateTime TimeAdded;
    public float DurationSeconds;
}

public class NotificationManager
{
    private readonly List<CordiNotification> _activeNotifications = new();
    private readonly UiTheme _theme;

    public NotificationManager()
    {
        _theme = new UiTheme();
    }

    public void Add(string title, string message, CordiNotificationType type = CordiNotificationType.Info, float duration = 5f)
    {
        lock (_activeNotifications)
        {
            _activeNotifications.Add(new CordiNotification
            {
                Title = title,
                Message = message,
                Type = type,
                TimeAdded = DateTime.Now,
                DurationSeconds = duration
            });
        }
    }

    public void Draw()
    {
        if (_activeNotifications.Count == 0) return;

        var viewport = ImGui.GetMainViewport();
        var padX = 20f * ImGuiHelpers.GlobalScale;
        var padY = 300f * ImGuiHelpers.GlobalScale;
        var startPos = new Vector2(viewport.WorkPos.X + viewport.WorkSize.X - padX, viewport.WorkPos.Y + viewport.WorkSize.Y - padY);


        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, _theme.CardBg);
        ImGui.PushStyleColor(ImGuiCol.Border, _theme.WindowBorder);
        ImGui.PushStyleColor(ImGuiCol.Text, _theme.Text);

        lock (_activeNotifications)
        {

            for (int i = _activeNotifications.Count - 1; i >= 0; i--)
            {
                var n = _activeNotifications[i];
                var elapsed = (DateTime.Now - n.TimeAdded).TotalSeconds;

                if (elapsed > n.DurationSeconds)
                {
                    _activeNotifications.RemoveAt(i);
                    continue;
                }


                float alpha = 1f;
                if (elapsed < 0.2f) alpha = (float)(elapsed / 0.2f);
                else if (elapsed > n.DurationSeconds - 0.5f) alpha = (float)((n.DurationSeconds - elapsed) / 0.5f);

                ImGui.SetNextWindowBgAlpha(alpha);

                var color = n.Type switch
                {
                    CordiNotificationType.Success => UiTheme.ColorSuccess,
                    CordiNotificationType.Error => UiTheme.ColorDanger,
                    CordiNotificationType.Warning => new Vector4(1f, 0.64f, 0f, 1f),
                    _ => _theme.Accent
                };


                ImGui.PushStyleColor(ImGuiCol.Border, color);

                string id = $"##notif{i}";
                ImGui.SetNextWindowPos(startPos, ImGuiCond.Always, new Vector2(1f, 1f));


                var width = 300f * ImGuiHelpers.GlobalScale;
                ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0f), new Vector2(width, 1000f));

                ImGui.Begin(id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove);


                var h = ImGui.GetWindowHeight();

                var titleColor = n.Type switch
                {
                    CordiNotificationType.Success => UiTheme.ColorSuccessText,
                    CordiNotificationType.Error => UiTheme.ColorDangerText,
                    CordiNotificationType.Warning => new Vector4(1f, 0.8f, 0.4f, 1f), // Lighter orange
                    _ => _theme.Accent
                };
                if (n.Type == CordiNotificationType.Info) titleColor = UiTheme.ColorSuccessText;








                if (n.Type == CordiNotificationType.Info) titleColor = _theme.SliderGrab;

                ImGui.TextColored(titleColor, n.Title);
                ImGui.Separator();
                ImGui.TextWrapped(n.Message);


                startPos.Y -= ImGui.GetWindowHeight() + 10f * ImGuiHelpers.GlobalScale;

                ImGui.End();
                ImGui.PopStyleColor();
            }
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
    }
}
