using HMUI;
using Zenject;
using UnityEngine;
using BeatSaberMarkupLanguage;

namespace RandomPlaylistMod.UI
{
    public class RandomPlaylistFlowCoordinator : FlowCoordinator
    {
        private RandomPlaylistUI _randomPlaylistUI;
        private MainFlowCoordinator _mainFlowCoordinator;
        private bool _isPresented;

        [Inject]
        public void Construct(RandomPlaylistUI randomPlaylistUI, MainFlowCoordinator mainFlowCoordinator)
        {
            _randomPlaylistUI = randomPlaylistUI;
            _mainFlowCoordinator = mainFlowCoordinator;
            Plugin.Log.Info("RandomPlaylistFlowCoordinator: Dependencies injected");
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            Plugin.Log.Info($"RandomPlaylistFlowCoordinator: DidActivate firstActivation={firstActivation}, addedToHierarchy={addedToHierarchy}, screenSystemEnabling={screenSystemEnabling}");
            
            if (firstActivation)
            {
                SetTitle("Random Playlist");
                showBackButton = true;
            }

            if (addedToHierarchy || _isPresented)
            {
                if (_randomPlaylistUI != null)
                {
                    ProvideInitialViewControllers(_randomPlaylistUI);
                }
                else
                {
                    Plugin.Log.Error("RandomPlaylistFlowCoordinator: _randomPlaylistUI is NULL!");
                }
            }
            
            _isPresented = true;
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            Plugin.Log.Info("RandomPlaylistFlowCoordinator: Back button pressed");
            
            if (_mainFlowCoordinator != null)
            {
                _mainFlowCoordinator.DismissFlowCoordinator(this);
            }
            else
            {
                Plugin.Log.Error("RandomPlaylistFlowCoordinator: _mainFlowCoordinator is NULL, trying fallback...");
                BeatSaberUI.MainFlowCoordinator?.DismissFlowCoordinator(this);
            }
        }
    }
}
