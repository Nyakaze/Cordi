using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Cordi.Services
{
    public class HonorificBridge : IDisposable
    {
        private const string LogSource = "Honorific";

        private readonly IDalamudPluginInterface _pluginInterface;
        private readonly CordiLogService _log;

        private readonly ICallGateSubscriber<int, string, object> _setCharacterTitle;
        private readonly ICallGateSubscriber<int, object> _clearCharacterTitle;

        public bool IsAvailable { get; private set; } = false;

        public HonorificBridge(IDalamudPluginInterface pi, CordiLogService log)
        {
            _pluginInterface = pi;
            _log = log;

            _setCharacterTitle = _pluginInterface.GetIpcSubscriber<int, string, object>("Honorific.SetCharacterTitle");
            _clearCharacterTitle = _pluginInterface.GetIpcSubscriber<int, object>("Honorific.ClearCharacterTitle");
        }

        public void SetTitle(IPlayerCharacter character, string title, bool isPrefix = false, System.Numerics.Vector3? color = null, System.Numerics.Vector3? glow = null, int? gradientColourSet = null, int? gradientAnimationStyle = null)
        {
            if (character == null) return;
            try
            {
                var dict = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["Title"] = title,
                    ["IsPrefix"] = isPrefix,
                };
                if (color != null) dict["Color"] = color;
                if (glow != null) dict["Glow"] = glow;
                if (gradientColourSet != null)
                {
                    dict["GradientColourSet"] = gradientColourSet;
                    if (color != null) dict["Color3"] = color;
                }
                if (gradientAnimationStyle != null) dict["GradientAnimationStyle"] = gradientAnimationStyle;
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);


                _setCharacterTitle.InvokeAction(0, json);
            }
            catch (Exception ex)
            {
                _log.Error(LogSource, "Honorific could not set the title", ex);
            }
        }

        public void ClearTitle(IPlayerCharacter character)
        {
            if (character == null) return;
            try
            {

                _clearCharacterTitle.InvokeAction(0);
            }
            catch (Exception ex)
            {
                _log.Info(LogSource, $"Honorific could not clear the title: {ex.Message}");
            }
        }

        public void Dispose()
        {

        }
    }
}
