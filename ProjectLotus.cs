using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using BepInEx;
using AmongUs.GameOptions;
using Lotus;
using Lotus.Addons;
using Lotus.API;
using Lotus.API.Reactive;
using Lotus.API.Reactive.HookEvents;
using Lotus.GameModes;
using Lotus.GUI.Menus;
using Lotus.GUI.Patches;
using Lotus.Managers;
using UnityEngine;
using VentLib;
using VentLib.Networking.Handshake;
using VentLib.Networking.RPC;
using VentLib.Utilities.Optionals;
using VentLib.Version;
using VentLib.Version.Git;
using VentLib.Version.Updater;
using VentLib.Version.Updater.Github;
using Version = VentLib.Version.Version;
using Lotus.Extensions;
using VentLib.Options.UI.Controllers;
using Lotus.Roles.Internals.Attributes;
using VentLib.Version.BuiltIn;
#if !DEBUG
using VentLib.Utilities.Debug.Profiling;
#endif

[assembly: AssemblyVersion(ProjectLotus.CompileVersion)]
namespace Lotus;

[BepInPlugin(Id, "Lotus", VisibleVersion)]
[BepInDependency(Vents.Id)]
[BepInProcess("Among Us.exe")]
public class ProjectLotus : BasePlugin, IGitVersionEmitter
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(ProjectLotus));
    private const string Id = "com.discussions.LotusContinued";
    public const string VisibleVersion = $"{MajorVersion}.{MinorVersion}.{PatchVersion}";
    public const string CompileVersion = $"{MajorVersion}.{MinorVersion}.{PatchVersion}.{BuildNumber}";

    public const string MajorVersion = "1";
    public const string MinorVersion = "1"; // Update with each release
    public const string PatchVersion = "0";
    public const string BuildNumber = "0863";

    public static string PluginVersion = typeof(ProjectLotus).Assembly.GetName().Version!.ToString();

    public readonly GitVersion CurrentVersion = new();

    public static readonly string ModName = "Project Lotus";
    public static readonly string ModColor = "#4FF918";
    public static readonly string DevVersionStr = "Dev August 3 2024";
    public static bool DevVersion = false;

    private static Harmony _harmony = null!;
    public static string CredentialsText = null!;

    public static ModUpdater ModUpdater = null!;

    public static bool FinishedLoading;
    public static bool AdvancedRoleAssignment = true;

    public ProjectLotus()
    {
#if DEBUG
        DevVersion = true;
        RpcMonitor.Enable();
        DebugExtensions.Start();
#endif
        Instance = this;

        VersionControl versionControl = ModVersion.VersionControl = VersionControl.For(this);
        versionControl.AddVersionReceiver(ReceiveVersion);
        PluginDataManager.TemplateManager.RegisterTag("lobby-join", "Tag for the template shown to players joining the lobby.");

        ModUpdater = ModUpdater.Default();
        ModUpdater.EstablishConnection();
        ModUpdater.RegisterReleaseCallback(BeginUpdate, true);

#if !DEBUG
        Profilers.Global.SetActive(false);
#endif
    }

    private void BeginUpdate(Release release)
    {
        UnityOptional<ModUpdateMenu>.Of(SplashPatch.ModUpdateMenu).Handle(o => o.Open(), () => SplashPatch.UpdateReady = true);
        ModUpdateMenu.AddUpdateItem("Lotus", null, ex => ModUpdater.Update(errorCallback: ex)!);
        Assembly ventAssembly = typeof(Vents).Assembly;

        if (release.ContainsDLL($"{ventAssembly.GetName().Name!}.dll"))
            ModUpdateMenu.AddUpdateItem("VentFrameworkContinued", null, ex => ModUpdater.Update(ventAssembly, ex)!);
    }

    public static NormalGameOptionsV08 NormalOptions => GameOptionsManager.Instance.currentNormalGameOptions;

    public static GameModeManager GameModeManager = null!;
    public static List<byte> ResetCamPlayerList = null!;
    public static ProjectLotus Instance = null!;

    public override void Load()
    {

        _harmony = new Harmony(Id);
        //Profilers.Global.SetActive(false);
        log.Info($"{Application.version}", "AmongUs Version");

        SettingsOptionController.Enable();
        GameModeManager = new GameModeManager();

        log.Info(CurrentVersion.ToString(), "GitVersion");

        // Setup, order matters here

        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        new GlobalRoleManager();
        GameModeManager.AddGamemodes();
        AddonManager.ImportAddons();
        GameModeManager.Setup();
        StaticEditor.Register(Assembly.GetExecutingAssembly());

        RoleOptionController.Enable();
        RoleOptionController.RemoveBuiltInTabs();

        FinishedLoading = true;
        log.High("Finished Initializing Project Lotus. Sending Post-Initialization Event");
        Hooks.ModHooks.LotusInitializedHook.Propagate(EmptyHookEvent.Hook);
    }

    public GitVersion Version() => CurrentVersion;

    public HandshakeResult HandshakeFilter(Version handshake)
    {
        if (handshake is NoVersion) return HandshakeResult.FailDoNothing;
        if (handshake is AmongUsMenuVersion) return HandshakeResult.FailDoNothing;
        if (handshake is SickoMenuVersion) return HandshakeResult.FailDoNothing;
        if (handshake is not GitVersion git) return HandshakeResult.DisableRPC;
        if (git.MajorVersion != CurrentVersion.MajorVersion && git.MinorVersion != CurrentVersion.MinorVersion) return HandshakeResult.FailDoNothing;
        return HandshakeResult.PassDoNothing;
    }

    private static void ReceiveVersion(Version version, PlayerControl player)
    {
        if (player == null) return;
        if (version is AmongUsMenuVersion)
        {
            PluginDataManager.BanManager.BanWithReason(player, "Cheating - Among Us Menu Auto Ban", $"{player.name} was banned because of the AmongUsMenu detection.");
            return;
        }
        if (version is SickoMenuVersion)
        {
            PluginDataManager.BanManager.BanWithReason(player, "Cheating - SickoMenu Auto Ban", $"{player.name} was banned because of the SickoMenu detection.");
            return;
        }
        if (version is not NoVersion)
        {
            //ModRPC rpc = Vents.FindRPC((uint)ModCalls.SendOptionPreview)!;
            //rpc.Send(new[] { player.GetClientId() }, new BatchList<Option>(OptionManager.GetManager().GetOptions()));
        }

        PluginDataManager.TemplateManager.GetTemplates("lobby-join")?.ForEach(t =>
        {
            if (player == null) return;
            t.SendMessage(PlayerControl.LocalPlayer, player);
        });

        Hooks.NetworkHooks.ReceiveVersionHook.Propagate(new ReceiveVersionHookEvent(player, version));
    }
}