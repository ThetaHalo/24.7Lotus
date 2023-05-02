using System;
using System.Linq;
using HarmonyLib;
using TOHTOR.API;
using TOHTOR.API.Odyssey;
using TOHTOR.API.Vanilla;
using TOHTOR.Extensions;
using TOHTOR.Options;
using TOHTOR.Roles.Internals;
using TOHTOR.Roles.Internals.Attributes;
using TOHTOR.Utilities;
using VentLib.Logging;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Version;
using Version = VentLib.Version.Version;

namespace TOHTOR.Patches.Intro;

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
class CoBeginPatch
{
    public static void Prefix()
    {
        Game.State = GameState.InIntro;
        VentLogger.Old("------------名前表示------------", "Info");
        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            VentLogger.Old($"{(pc.AmOwner ? "[*]" : ""),-3}{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.cosmetics.nameText.text}({Palette.ColorNames[pc.Data.DefaultOutfit.ColorId].ToString().Replace("Color", "")})", "Info");
            pc.cosmetics.nameText.text = pc.name;
        }
        VentLogger.Old("----------役職割り当て----------", "Info");
        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            VentLogger.Old($"{(pc.AmOwner ? "[*]" : ""),-3}{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.GetAllRoleName()}", "Info");
        }
        VentLogger.Old("--------------環境--------------", "Info");
        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            try
            {
                var text = pc.AmOwner ? "[*]" : "   ";
                text += $"{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.GetClient()?.PlatformData?.Platform.ToString()?.Replace("Standalone", ""),-11}";

                Version version = ModVersion.VersionControl.GetPlayerVersion(pc.PlayerId);
                text += version == new NoVersion() ? ":Vanilla" : $":Mod({version})";
                VentLogger.Old(text, "Info");
            }
            catch (Exception ex)
            {
                VentLogger.Error(ex.ToString(), "Platform");
            }
        }
        VentLogger.Old("------------基本設定------------", "Info");
        var tmp = GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split("\r\n").Skip(1);
        foreach (var t in tmp) VentLogger.Old(t, "Info");
        VentLogger.Old("------------詳細設定------------", "Info");
        VentLogger.Old($"プレイヤー数: {PlayerControl.AllPlayerControls.Count}人", "Info");
        //PlayerControl.AllPlayerControls.ToArray().Do(x => TOHPlugin.PlayerStates[x.PlayerId].InitTask(x));

        GameStates.InGame = true;
    }

    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GeneralOptions.GameplayOptions.ForceNoVenting) Game.GetAlivePlayers().Where(p => !p.GetCustomRole().BaseCanVent).ForEach(VentApi.ForceNoVenting);
        ActionHandle handle = ActionHandle.NoInit();
        Game.TriggerForAll(RoleActionType.RoundStart, ref handle, true);
    }
}