using System.Diagnostics.CodeAnalysis;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.Chat;
using Lotus.Extensions;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Managers.History.Events;
using Lotus.Roles.Interactions.Interfaces;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.RoleGroups.Vanilla;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.Game;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using static Lotus.Options.GeneralOptionTranslations;
using static Lotus.Roles.RoleGroups.Crew.Medic.MedicTranslations;

namespace Lotus.Roles.RoleGroups.Crew;

[Localized("Roles")]
public class Medic: Crewmate
{
    private static Color crossColor = new(0.2f, 0.49f, 0f);
    private GuardMode mode;
    private byte guardedPlayer = byte.MaxValue;

    private bool targetLockedIn;
    private bool confirmedVote;

    private Remote<IndicatorComponent>? protectedIndicator;

    private static string CrossText = "<size=3><b>+</b></size>";

    [RoleAction(RoleActionType.RoundEnd)]
    private void RoundEndMessage()
    {
        confirmedVote = false;
        if (guardedPlayer == byte.MaxValue) CHandler(MedicHelpMessage).Send(MyPlayer);
        else if (mode is GuardMode.AnyMeeting) CHandler(ProtectingMessage.Formatted(Players.FindPlayerById(guardedPlayer)?.name)).Send(MyPlayer);
    }

    [RoleAction(RoleActionType.AnyInteraction)]
    protected void ProtectTarget(PlayerControl killer, PlayerControl target, Interaction interaction, ActionHandle handle)
    {
        if (Game.State is not GameState.Roaming) return;
        if (guardedPlayer != target.PlayerId) return;

        if (interaction.Intent() is not (IHostileIntent or IFatalIntent)) return;
        handle.Cancel();
        Game.MatchData.GameHistory.AddEvent(new PlayerSavedEvent(target, MyPlayer, killer));
    }

    [SuppressMessage("ReSharper", "AssignmentInConditionalExpression")]
    [RoleAction(RoleActionType.MyVote)]
    private void HandleMedicVote(Optional<PlayerControl> votedPlayer, ActionHandle handle)
    {
        if (confirmedVote) return;
        // If guarded player is selected, and mode is any meeting then skip
        if (targetLockedIn && guardedPlayer != byte.MaxValue && mode is not GuardMode.AnyMeeting) return;

        handle.Cancel();

        if (confirmedVote = !votedPlayer.Exists())
        {
            guardedPlayer = byte.MaxValue;
            CHandler(ReturnToNormalVoting.Formatted(NoOneText)).Send(MyPlayer);
            return;
        }

        PlayerControl voted = votedPlayer.Get();
        byte player = voted.PlayerId;

        if (player == MyPlayer.PlayerId) return;

        if (confirmedVote = guardedPlayer == player)
        {
            targetLockedIn = true;
            CHandler(ReturnToNormalVoting.Formatted(Players.FindPlayerById(guardedPlayer)?.name)).Send(MyPlayer);
            return;
        }

        protectedIndicator?.Delete();
        guardedPlayer = player;
        protectedIndicator = voted.NameModel().GCH<IndicatorHolder>().Add(new SimpleIndicatorComponent("<b>+</b>", crossColor, Game.IgnStates, MyPlayer));
        Game.GetDeadPlayers().ForEach(p => protectedIndicator?.Get().AddViewer(p));

        CHandler(SelectedPlayerMessage.Formatted(Players.FindPlayerById(guardedPlayer)?.name)).Send(MyPlayer);
    }

    [RoleAction(RoleActionType.AnyExiled)]
    [RoleAction(RoleActionType.Disconnect)]
    [RoleAction(RoleActionType.AnyDeath)]
    private void CheckForDisconnectAndDeath(PlayerControl player, ActionHandle handle)
    {
        if (player.PlayerId != guardedPlayer) return;
        bool resetGuard = handle.ActionType is RoleActionType.Disconnect;
        resetGuard = resetGuard || handle.ActionType is RoleActionType.AnyDeath && mode is GuardMode.OnDeath;

        protectedIndicator?.Delete();
        if (!resetGuard) return;

        targetLockedIn = false;
        guardedPlayer = byte.MaxValue;
    }

    public ChatHandler CHandler(string message) => new ChatHandler()
        .Title(t => t.PrefixSuffix(CrossText).Color(RoleColor).Text(RoleName).Build())
        .LeftAlign().Message(message);

    protected override RoleModifier Modify(RoleModifier roleModifier) => base.Modify(roleModifier).RoleColor(new Color(0f, 0.4f, 0f));

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub
                .KeyName("Change Guarded Player", MedicOptionTranslations.ChangeGuardedPlayer)
                .Value(v => v.Text(MedicOptionTranslations.OnDeathValue).Value(2).Build())
                .Value(v => v.Text(MedicOptionTranslations.MeetingsValue).Value(1).Build())
                .Value(v => v.Text(MedicOptionTranslations.NeverValue).Value(0).Build())
                .BindInt(o => mode = (GuardMode)o)
                .Build());

    protected enum GuardMode
    {
        Never,
        AnyMeeting,
        OnDeath
    }

    [Localized(nameof(Medic))]
    internal static class MedicTranslations
    {
        [Localized(nameof(ProtectingMessage))]
        public static string ProtectingMessage = "You are currently protecting: {0}";

        [Localized(nameof(MedicHelpMessage))]
        public static string MedicHelpMessage = "You are a medic! Your duty: to save innocent lives! Vote a player to protect next round! Alternatively, you can skip here to return to normal voting.";

        [Localized(nameof(SelectedPlayerMessage))]
        public static string SelectedPlayerMessage = "You have decided to protect {0}. Vote them again to confirm your choice, or Skip to return to normal voting.";

        [Localized(nameof(ReturnToNormalVoting))]
        public static string ReturnToNormalVoting = "You are now protecting {0}. Your next vote works as normal.";

        [Localized(ModConstants.Options)]
        public static class MedicOptionTranslations
        {
            [Localized(nameof(ChangeGuardedPlayer))]
            public static string ChangeGuardedPlayer = "Change Guarded Player";

            [Localized(nameof(OnDeathValue))]
            public static string OnDeathValue = "After Death";

            [Localized(nameof(MeetingsValue))]
            public static string MeetingsValue = "Meetings";

            [Localized(nameof(NeverValue))]
            public static string NeverValue = "Never";
        }
    }
}