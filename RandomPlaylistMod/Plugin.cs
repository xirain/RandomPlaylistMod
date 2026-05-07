



using IPA;
using RandomPlaylistMod.Managers;
using RandomPlaylistMod.UI;
using SiraUtil.Zenject;
using Zenject;
using IPALogger = IPA.Logging.Logger;

namespace RandomPlaylistMod
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        [Init]
        public Plugin(IPALogger logger, Zenjector zenjector)
        {
            Instance = this;
            Log = logger;
            zenjector.Install<AppInstaller>(Location.App);
            zenjector.Install<MenuInstaller>(Location.Menu);
        }

        [OnStart]
        public void OnApplicationStart()
        {
            Log.Info("RandomPlaylistMod: OnApplicationStart called");
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            Log.Info("RandomPlaylistMod exiting");
        }
    }

    public class AppInstaller : Installer
    {
        public override void InstallBindings()
        {
            // 只绑定业务逻辑类
            Container.Bind<PlaylistManager>().AsSingle();
            Container.Bind<SongSelector>().AsSingle();
            Container.Bind<TimeManager>().AsSingle();
            Container.Bind<PlaySessionManager>().AsSingle();
        }
    }

    public class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Plugin.Log.Info("MenuInstaller: Installing bindings...");
            
            // UI 类使用 SiraUtil 的特殊方法创建
            Container.Bind<RandomPlaylistUI>().FromNewComponentAsViewController().AsSingle();
            
            // FlowCoordinator 需要在新 GameObject 上创建
            Container.Bind<RandomPlaylistFlowCoordinator>()
                .FromNewComponentOnNewGameObject()
                .AsSingle();
            
            Container.BindInterfacesTo<MenuButtonManager>().AsSingle();
            
            Plugin.Log.Info("MenuInstaller: Bindings installed");
        }
    }
}
