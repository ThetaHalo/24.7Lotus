using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using TownOfHost.Extensions;
using TownOfHost.Managers;
using TownOfHost.Options;
using TownOfHost.Patches.Chat;
using TownOfHost.ReduxOptions;
using TownOfHost.Roles;
using UnityEngine;
using VentLib.Logging;
using static TownOfHost.Managers.Translator;

namespace TownOfHost.Patches.HUD;

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
class CheckForEndVotingPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        try
        {
            List<MeetingHud.VoterState> statesList = new();
            if (__instance.playerStates.Any(ps => !(ps.AmDead || ps.DidVote))) return false;

            GameData.PlayerInfo? exiledPlayer = PlayerControl.LocalPlayer.Data;
            bool tie = false;

            for (var i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea ps = __instance.playerStates[i];
                if (ps == null) continue;
                VentLogger.Old(string.Format("{0,-2}{1}:{2,-3}{3}", ps.TargetPlayerId, Utils.PadRightV2($"({Utils.GetVoteName(ps.TargetPlayerId)})", 40), ps.VotedFor, $"({Utils.GetVoteName(ps.VotedFor)})"), "Vote");
                var voter = Utils.GetPlayerById(ps.TargetPlayerId);
                if (voter == null || voter.Data == null || voter.Data.Disconnected) continue;
                statesList.Add(new MeetingHud.VoterState()
                {
                    VoterId = ps.TargetPlayerId,
                    VotedForId = ps.VotedFor
                });
                if (IsMayor(ps.TargetPlayerId))//Mayorの投票数
                {
                    for (var i2 = 0; i2 < StaticOptions.MayorAdditionalVote; i2++)
                    {
                        statesList.Add(new MeetingHud.VoterState()
                        {
                            VoterId = ps.TargetPlayerId,
                            VotedForId = ps.VotedFor
                        });
                    }
                }
            }
            MeetingHud.VoterState[] states = statesList.ToArray();

            var VotingData = __instance.CustomCalculateVotes();
            byte exileId = byte.MaxValue;
            int max = 0;
            VentLogger.Old("===追放者確認処理開始===", "Vote");
            foreach (var data in VotingData)
            {
                VentLogger.Old($"{data.Key}({Utils.GetVoteName(data.Key)}):{data.Value}票", "Vote");
                if (data.Value > max)
                {
                    VentLogger.Old(data.Key + "番が最高値を更新(" + data.Value + ")", "Vote");
                    exileId = data.Key;
                    max = data.Value;
                    tie = false;
                }
                else if (data.Value == max)
                {
                    VentLogger.Old(data.Key + "番が" + exileId + "番と同数(" + data.Value + ")", "Vote");
                    exileId = byte.MaxValue;
                    tie = true;
                }
                VentLogger.Old($"exileId: {exileId}, max: {max}票", "Vote");
            }

            VentLogger.Old($"追放者決定: {exileId}({Utils.GetVoteName(exileId)})", "Vote");

            exiledPlayer = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => !tie && info.PlayerId == exileId);
            AntiBlackout.SaveCosmetics();
            GameData.PlayerInfo? fakeExiled = AntiBlackout.CreateFakePlayer(exiledPlayer);

            if (AntiBlackout.OverrideExiledPlayer)
                AntiBlackout.ExiledPlayer = exiledPlayer;

            if (fakeExiled == null)
            {
                __instance.RpcVotingComplete(states, null, true);
                return false;
            }

            AntiBlackout.SendGameData();
            __instance.RpcVotingComplete(states, fakeExiled, tie); //通常処理
            return false;
        }
        catch (Exception ex)
        {
            Logger.SendInGame(string.Format(GetString("Error.MeetingException"), ex.Message), true);
            throw;
        }
    }
    public static bool IsMayor(byte id)
    {
        var player = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(pc => pc.PlayerId == id);
        return player != null && player.Is(Mayor.Ref<Mayor>());
    }
}

static class ExtendedMeetingHud
{
    public static Dictionary<byte, int> CustomCalculateVotes(this MeetingHud __instance)
    {
        VentLogger.Old("CustomCalculateVotes開始", "Vote");
        Dictionary<byte, int> dic = new();
        //| 投票された人 | 投票された回数 |
        for (int i = 0; i < __instance.playerStates.Length; i++)
        {
            PlayerVoteArea ps = __instance.playerStates[i];
            if (ps == null) continue;
            if (ps.VotedFor is not ((byte)252) and not byte.MaxValue and not ((byte)254))
            {
                int VoteNum = 1;
                //投票を1追加 キーが定義されていない場合は1で上書きして定義
                dic[ps.VotedFor] = !dic.TryGetValue(ps.VotedFor, out int num) ? VoteNum : num + VoteNum;
            }
        }
        return dic;
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
class MeetingHudStartPatch
{
    public static void Prefix(MeetingHud __instance)
    {
        Game.State = GameState.InMeeting;
        VentLogger.Old("------------会議開始------------", "Phase");
        ActionHandle handle = ActionHandle.NoInit();
        Game.TriggerForAll(RoleActionType.RoundEnd, ref handle, false);
        ChatUpdatePatch.DoBlockChat = true;
        GameStates.AlreadyDied |= GameData.Instance.AllPlayers.ToArray().Any(x => x.IsDead);
        MeetingStates.MeetingCalled = true;
        Game.RenderAllForAll();
        "Meeting Call Done".DebugLog();
    }
    public static void Postfix(MeetingHud __instance)
    {
        SoundManager.Instance.ChangeMusicVolume(0f);
        if (StaticOptions.SyncButtonMode)
        {
            Utils.SendMessage(string.Format(GetString("Message.SyncButtonLeft"), StaticOptions.SyncedButtonCount - OldOptions.UsedButtonCount));
            VentLogger.Old("緊急会議ボタンはあと" + (StaticOptions.SyncedButtonCount - OldOptions.UsedButtonCount) + "回使用可能です。", "SyncButtonMode");
        }
        if (AntiBlackout.OverrideExiledPlayer)
            Utils.SendMessage(GetString("Warning.OverrideExiledPlayer"));

        if (AmongUsClient.Instance.AmHost)
            DTask.Schedule(() => ChatUpdatePatch.DoBlockChat = false, 3f);
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
class MeetingHudUpdatePatch
{
    public static void Postfix(MeetingHud __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Input.GetMouseButtonUp(1) && Input.GetKey(KeyCode.LeftControl))
        {
            __instance.playerStates.DoIf(x => x.HighlightedFX.enabled, x =>
            {
                var player = Utils.GetPlayerById(x.TargetPlayerId);
                player.RpcExileV2();
                TOHPlugin.PlayerStates[player.PlayerId].deathReason = PlayerStateOLD.DeathReason.Execution;
                TOHPlugin.PlayerStates[player.PlayerId].SetDead();
                Utils.SendMessage(string.Format(GetString("Message.Executed"), player.Data.PlayerName));
                VentLogger.Old($"{player.GetNameWithRole()}を処刑しました", "Execution");
            });
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CoStartMeeting))]
public class HostMeetingPatch
{
    public static void Prefix(ShipStatus __instance, [HarmonyArgument(0)] PlayerControl reporter)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        reporter.GetDynamicName().RenderFor(PlayerControl.LocalPlayer);
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetHighlighted))]
class SetHighlightedPatch
{
    public static bool Prefix(PlayerVoteArea __instance, bool value)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!__instance.HighlightedFX) return false;
        __instance.HighlightedFX.enabled = value;
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
class MeetingHudOnDestroyPatch
{
    public static void Prefix()
    {
        AntiBlackout.SetIsDead();
        Thread.Sleep(30);
    }

    public static void Postfix()
    {
        Game.State = GameState.Roaming;
        MeetingStates.FirstMeeting = false;
        VentLogger.Old("------------会議終了------------", "Phase");
        if (AmongUsClient.Instance.AmHost)
        {
            Game.GetAllPlayers().Do(pc => RandomSpawn.CustomNetworkTransformPatch.NumOfTP[pc.PlayerId] = 0);
        }
        ActionHandle handle = ActionHandle.NoInit();
        Game.TriggerForAll(RoleActionType.RoundStart, ref handle, false);
    }
}