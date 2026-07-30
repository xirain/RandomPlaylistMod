



using IPA;
using RandomPlaylistMod.Managers;
using RandomPlaylistMod.UI;
using SiraUtil.Zenject;
using System;
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
            zenjector.Install<GameInstaller>(Location.StandardPlayer);
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
            Container.Bind<PlaylistManager>().AsSingle();
            Container.Bind<SongSelector>().AsSingle();
            Container.Bind<TimeManager>().AsSingle();
            Container.Bind<PlaySessionManager>().AsSingle();
            // 游玩中按 Y/B 收藏当前歌曲到固定歌单
            Container.Bind<FavoriteManager>().AsSingle();
            // Phase 2: 数据持久化与分享
            Container.Bind<HistoryManager>().AsSingle();
            Container.Bind<ShareImageGenerator>().AsSingle();
        }
    }

    public class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Plugin.Log.Info("MenuInstaller: Installing bindings...");
            
            // UI 类使用 SiraUtil 的特殊方法创建
            Container.Bind<RandomPlaylistUI>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<SessionSummaryView>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<HistoryView>().FromNewComponentAsViewController().AsSingle();
            
            // FlowCoordinator 需要在新 GameObject 上创建
            Container.Bind<RandomPlaylistFlowCoordinator>()
                .FromNewComponentOnNewGameObject()
                .AsSingle();
            
            Container.BindInterfacesTo<MenuButtonManager>().AsSingle();
            
            Plugin.Log.Info("MenuInstaller: Bindings installed");
        }
    }

    /// <summary>
    /// 游戏场景安装器 — 将 SessionHUDView 注入到游戏关卡场景
    /// 参考 Enhancements mod 的 XGameInstaller 架构
    /// </summary>
    public class GameInstaller : Installer
    {
        public override void InstallBindings()
        {
            Plugin.Log.Info("GameInstaller: Installing bindings...");

            Container.Bind<SessionHUDView>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();

            // 游玩中监听手柄 Y/B 键，按下即收藏当前歌曲
            Container.Bind<GameplayFavoriteInput>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();

            Plugin.Log.Info("GameInstaller: Bindings installed");
        }
    }
}
