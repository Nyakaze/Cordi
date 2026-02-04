using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace Cordi.Services
{
    public class HonorificBridge : IDisposable
    {
        private readonly IDalamudPluginInterface _pluginInterface;


        private readonly ICallGateSubscriber<int, string, object> _setCharacterTitle;
        private readonly ICallGateSubscriber<int, object> _clearCharacterTitle;

        public bool IsAvailable { get; private set; } = false;

        public HonorificBridge(IDalamudPluginInterface pi)
        {
            _pluginInterface = pi;


            _setCharacterTitle = _pluginInterface.GetIpcSubscriber<int, string, object>("Honorific.SetCharacterTitle");
            _clearCharacterTitle = _pluginInterface.GetIpcSubscriber<int, object>("Honorific.ClearCharacterTitle");
        }

        public void SetTitle(IPlayerCharacter character, string title, bool isPrefix = false, System.Numerics.Vector3? color = null, System.Numerics.Vector3? glow = null)
        {
            if (character == null) return;
            try
            {

                var data = new
                {
                    Title = title,
                    IsPrefix = isPrefix,
                    Color = color,
                    Glow = glow
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);


                _setCharacterTitle.InvokeAction(0, json);
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Honorific IPC SetTitle failed: {ex.Message}");
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
                Service.Log.Info($"Honorific IPC ClearTitle failed: {ex.Message}");
            }
        }

        public void Dispose()
        {

        }
    }
}
