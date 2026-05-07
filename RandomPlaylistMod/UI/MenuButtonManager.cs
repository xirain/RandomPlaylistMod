using System;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using Zenject;
using HMUI;

namespace RandomPlaylistMod.UI
{
    public class MenuButtonManager : IInitializable, IDisposable
    {
        private readonly RandomPlaylistUI _ui;
        private readonly RandomPlaylistFlowCoordinator _flowCoordinator;
        private readonly MainFlowCoordinator _mainFlowCoordinator;
        private MenuButton _menuButton;

        public MenuButtonManager(
            RandomPlaylistUI ui,
            RandomPlaylistFlowCoordinator flowCoordinator,
            MainFlowCoordinator mainFlowCoordinator)
        {
            _ui = ui;
            _flowCoordinator = flowCoordinator;
            _mainFlowCoordinator = mainFlowCoordinator;
        }

        public void Initialize()
        {
            Plugin.Log.Info("MenuButtonManager: Initializing...");

            try
            {
                _menuButton = new MenuButton(
                    "Random Playlist",
                    "Start a random playlist session",
                    OnButtonClick);

                MenuButtons.Instance.RegisterButton(_menuButton);
                Plugin.Log.Info("MenuButtonManager: Button registered successfully!");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"MenuButtonManager: Failed to register button - {ex.Message}");
                Plugin.Log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        private void OnButtonClick()
        {
            Plugin.Log.Info("MenuButtonManager: Button clicked!");

            try
            {
                if (_flowCoordinator == null)
                {
                    Plugin.Log.Error("MenuButtonManager: _flowCoordinator is NULL!");
                    return;
                }
                
                if (_mainFlowCoordinator == null)
                {
                    Plugin.Log.Error("MenuButtonManager: _mainFlowCoordinator is NULL!");
                    return;
                }
                
                Plugin.Log.Info("MenuButtonManager: Presenting flow coordinator using injected MainFlowCoordinator...");
                _mainFlowCoordinator.PresentFlowCoordinator(_flowCoordinator);
                Plugin.Log.Info("MenuButtonManager: PresentFlowCoordinator called successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"MenuButtonManager: Failed to show menu - {ex.Message}");
                Plugin.Log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_menuButton != null && MenuButtons.Instance != null)
                {
                    MenuButtons.Instance.UnregisterButton(_menuButton);
                    Plugin.Log.Info("MenuButtonManager: Button unregistered.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"MenuButtonManager: Dispose error - {ex.Message}");
            }
        }
    }
}
