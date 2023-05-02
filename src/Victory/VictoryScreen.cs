using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using TOHTOR.API;
using TOHTOR.API.Odyssey;
using TOHTOR.Extensions;
using TOHTOR.Player;
using TOHTOR.Roles;
using TOHTOR.RPC;
using VentLib.Logging;
using VentLib.Utilities.Extensions;

// ReSharper disable ConvertIfStatementToSwitchStatement

namespace TOHTOR.Victory;

public static class VictoryScreen
{
    public static void ShowWinners(List<PlayerControl> winners, GameOverReason reason)
    {
        List<FrozenPlayer> winnerRoles = Game.GameHistory.LastWinners = winners.Select(w => Game.GameHistory.FrozenPlayers[w.GetGameID()]).ToList();
        VentLogger.Info($"Setting Up Win Screen | Winners: {winnerRoles.StrJoin()}");

        bool impostorsWin = IsImpostorsWin(reason);

        List<PlayerControl> losers = Game.GetAllPlayers().ToList();

        winners.Do(winner =>
        {
            if (impostorsWin && !winner.Data.Role.IsImpostor) winner.CRpcSetRole(RoleTypes.ImpostorGhost);
            if (!impostorsWin && winner.Data.Role.IsImpostor) winner.CRpcSetRole(RoleTypes.CrewmateGhost);
            losers.RemoveAll(p => p.PlayerId == winner.PlayerId);
        });

        if (winners.Any(p => p.IsHost())) winners.Do(p => p.SetRole(impostorsWin ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost));

        losers.Do(loser =>
        {
            if (impostorsWin && loser.Data.Role.IsImpostor) loser.CRpcSetRole(RoleTypes.CrewmateGhost);
            if (!impostorsWin && !loser.Data.Role.IsImpostor) loser.CRpcSetRole(RoleTypes.ImpostorGhost);
        });

        if (winners.Any(p => p.IsHost())) losers.Do(p => p.SetRole(impostorsWin ? RoleTypes.CrewmateGhost : RoleTypes.ImpostorGhost));
    }

    private static bool IsImpostorsWin(GameOverReason reason)
    {
        return reason switch
        {
            GameOverReason.HumansByVote => false,
            GameOverReason.HumansByTask => false,
            GameOverReason.ImpostorByVote => true,
            GameOverReason.ImpostorByKill => true,
            GameOverReason.ImpostorBySabotage => true,
            GameOverReason.ImpostorDisconnect => false,
            GameOverReason.HumansDisconnect => true,
            GameOverReason.HideAndSeek_ByTimer => false,
            GameOverReason.HideAndSeek_ByKills => true,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }
}