using System;
using System.Collections.Generic;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Extensions;
using Lotus.Options;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities.Extensions;
using static Lotus.Roles.RoleGroups.Impostors.Conman.ConmanTranslations.ConmanOptionTranslations;
using Lotus.API.Player;
using VentLib.Utilities.Optionals;

namespace Lotus.Roles.RoleGroups.Impostors;

public class Conman : Impostor
{
    private int discussionTimeDecrease;
    private int votingTimeDecrease;
    private bool affectAnonymousMeeting;
    private bool triggerAbility;
    [NewOnSetup] private HashSet<byte> killedPlayers = null!;

    [RoleAction(LotusActionType.RoundStart)]
    private void ResetKilledPlayers() => killedPlayers.Clear();

    [RoleAction(LotusActionType.ReportBody, ActionFlag.GlobalDetector)]
    public void ReportBodyAbility(Optional<NetworkedPlayerInfo> reported) => triggerAbility = reported.Exists() ? killedPlayers.Contains(reported.Get().PlayerId) : false;

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        bool killed = base.TryKill(target);
        if (!killed) return false;
        killedPlayers.Add(target.PlayerId);
        return true;
    }

    [RoleAction(LotusActionType.RoundEnd)]
    public void SabotageMeeting()
    {
        List<GameOptionOverride> overrides = new();
        if (affectAnonymousMeeting) overrides.Add(new GameOptionOverride(Override.AnonymousVoting, !AUSettings.AnonymousVotes()));
        overrides.Add(new GameOptionOverride(Override.DiscussionTime, Math.Max(AUSettings.DiscussionTime() - discussionTimeDecrease, 1)));
        overrides.Add(new GameOptionOverride(Override.VotingTime, Math.Max(AUSettings.DiscussionTime() - votingTimeDecrease, 1)));
        Players.GetAllPlayers().ForEach(p => p.PrimaryRole().SyncOptions(overrides));
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        AddKillCooldownOptions(base.RegisterOptions(optionStream))
            .SubOption(sub => sub.KeyName("Discussion Time Decrease", DiscussionTimeDecrease)
                .AddIntRange(0, 120, 5, 6, GeneralOptionTranslations.SecondsSuffix)
                .BindInt(f => discussionTimeDecrease = f)
                .Build())
            .SubOption(sub => sub.KeyName("Voting Time Decrease", VotingTimeDecrease)
                .AddIntRange(0, 120, 5, 3, GeneralOptionTranslations.SecondsSuffix)
                .BindInt(f => votingTimeDecrease = f)
                .Build())
            .SubOption(sub => sub.KeyName("Affect Anonymous Voting", AffectAnonymousVoting)
                .AddOnOffValues()
                .BindBool(b => affectAnonymousMeeting = b)
                .Build());

    [Localized(nameof(Conman))]
    internal static class ConmanTranslations
    {
        [Localized(ModConstants.Options)]
        internal static class ConmanOptionTranslations
        {
            [Localized(nameof(DiscussionTimeDecrease))]
            public static string DiscussionTimeDecrease = "Discussion Time Decrease";

            [Localized(nameof(VotingTimeDecrease))]
            public static string VotingTimeDecrease = "Voting Time Decrease";

            [Localized(nameof(AffectAnonymousVoting))]
            public static string AffectAnonymousVoting = "Affect Anonymous Voting";
        }
    }
}