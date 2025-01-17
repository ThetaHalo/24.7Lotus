﻿using System;
using AmongUs.GameOptions;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.Chat;
using Lotus.Extensions;
using Lotus.Logging;
using Lotus.Managers;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Trackers;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using Lotus.GameModes.Standard;
using Lotus.API.Vanilla.Meetings;
using Lotus.Roles.Managers.Interfaces;
using System.Linq;

namespace Lotus.Roles.Subroles;

public class Guesser : CustomRole
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Guesser));
    private MeetingPlayerSelector voteSelector = new();

    private int guessesPerMeeting;
    private bool hasMadeGuess;
    private byte guessingPlayer = byte.MaxValue;
    private bool skippedVote;
    private CustomRole? guessedRole;
    private int guessesThisMeeting;

    protected int CorrectGuesses;
    protected string? GuesserMessage;

    [RoleAction(LotusActionType.RoundStart)]
    [RoleAction(LotusActionType.RoundEnd)]
    public void ResetPreppedPlayer()
    {
        hasMadeGuess = false;
        voteSelector.Reset();
        guessingPlayer = byte.MaxValue;
        skippedVote = false;
        guessedRole = null;
        GuesserMessage = null;
        guessesThisMeeting = 0;
    }

    [RoleAction(LotusActionType.Vote)]
    public void SelectPlayerToGuess(Optional<PlayerControl> player, MeetingDelegate _, ActionHandle handle)
    {
        if (skippedVote || hasMadeGuess) return;
        handle.Cancel();
        VoteResult result = voteSelector.CastVote(player);
        switch (result.VoteResultType)
        {
            case VoteResultType.None:
                break;
            case VoteResultType.Skipped:
                skippedVote = true;
                break;
            case VoteResultType.Selected:
                guessingPlayer = result.Selected;
                GuesserHandler(Translations.PickedPlayerText.Formatted(Players.FindPlayerById(result.Selected)?.name)).Send(MyPlayer);
                break;
            case VoteResultType.Confirmed:
                if (guessedRole == null)
                {
                    voteSelector.Reset();
                    voteSelector.CastVote(player);
                }
                else hasMadeGuess = true;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!hasMadeGuess) return;

        if (++guessesThisMeeting < guessesPerMeeting)
        {
            hasMadeGuess = false;
            voteSelector.Reset();
        }

        PlayerControl? guessed = Players.FindPlayerById(guessingPlayer);
        if (guessed == null || guessedRole == null)
        {
            GuesserHandler(Translations.ErrorCompletingGuess).Send(MyPlayer);
            ResetPreppedPlayer();
            return;
        }

        if (guessed.PrimaryRole().GetType() == guessedRole.GetType())
        {
            GuesserMessage = Translations.GuessAnnouncementMessage.Formatted(guessed.name);
            MyPlayer.InteractWith(guessed, LotusInteraction.FatalInteraction.Create(this));
            CorrectGuesses++;
        }
        else HandleBadGuess();
    }

    protected virtual void HandleBadGuess()
    {
        GuesserMessage = Translations.GuessAnnouncementMessage.Formatted(MyPlayer.name);
        MyPlayer.InteractWith(MyPlayer, LotusInteraction.FatalInteraction.Create(this));
    }

    [RoleAction(LotusActionType.MeetingEnd, ActionFlag.WorksAfterDeath)]
    public void CheckRevive()
    {
        if (GuesserMessage != null) GuesserHandler(GuesserMessage).Send();
    }

    [RoleAction(LotusActionType.Chat)]
    public void DoGuesserVoting(string message, GameState state, bool isAlive)
    {
        if (!isAlive) return;
        if (state is not GameState.InMeeting) return;
        log.Debug($"Message: {message} - Guessing player: {guessingPlayer}");
        if (guessingPlayer == byte.MaxValue) return;
        if (!(message.StartsWith("/role") || message.StartsWith("/r"))) return;
        string[] split = message.Replace("/role", "/r").Split(" ");
        if (split.Length == 1)
        {
            GuesserHandler(Translations.TypeRText).Send(MyPlayer);
            return;
        }

        string roleName = split[1..].Fuse(" ").Trim();
        var allRoles = IRoleManager.Current.AllCustomRoles().Where(r => r.Count > 0 && r.Chance > 0);
        Optional<CustomRole> role = allRoles.FirstOrOptional(r => string.Equals(r.RoleName, roleName, StringComparison.CurrentCultureIgnoreCase))
            .CoalesceEmpty(() => allRoles.FirstOrOptional(r => r.RoleName.ToLower().Contains(roleName.ToLower())));
        log.Debug($"c4 - exists: {role.Exists()} name: {(role.Exists() ? role.Get().RoleName : roleName)}");
        if (!role.Exists())
        {
            GuesserHandler(Translations.UnknownRole.Formatted(roleName)).Send(MyPlayer);
            return;
        }

        guessedRole = role.Get();
        GuesserHandler(Translations.PickedRoleText.Formatted(Players.FindPlayerById(guessingPlayer)?.name, guessedRole.RoleName)).Send(MyPlayer);
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.KeyName("Guesses per Meeting", Translations.Options.GuesserPerMeeting)
                .AddIntRange(1, 10, 1, 0)
                .BindInt(i => guessesPerMeeting = i)
                .Build());

    protected ChatHandler GuesserHandler(string message) => ChatHandler.Of(message, RoleColor.Colorize(Translations.GuesserTitle)).LeftAlign();

    [Localized(nameof(Guesser))]
    public static class Translations
    {
        [Localized(nameof(Guesser))]
        public static string GuesserTitle = "Guesser";

        [Localized(nameof(PickedRoleText))]
        public static string PickedRoleText = "You are about to guess {0} as {1}. If you are certain about this, vote {0} again to finalize your guess. Otherwise you can pick another player by voting a different player.. OR pick a different role by typing /r [rolename]";

        [Localized(nameof(PickedPlayerText))]
        public static string PickedPlayerText = "You are guessing {0}'s role. To guess their role type /r [rolename].";

        [Localized(nameof(TypeRText))]
        public static string TypeRText = "Please type /r [roleName] to guess that role.";

        [Localized(nameof(UnknownRole))]
        public static string UnknownRole = "Unknown role {0}. You can use /perc to view all enabled roles.";

        [Localized(nameof(FinishedGuessingText))]
        public static string FinishedGuessingText = "You have confirmed your guess. If you are not dead, you may now vote normally.";

        [Localized(nameof(ErrorCompletingGuess))]
        public static string ErrorCompletingGuess = "Error completing guess. You may try and guess again.";

        [Localized(nameof(GuessAnnouncementMessage))]
        public static string GuessAnnouncementMessage = "The guesser has made a guess. {0} died.";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            public static string GuesserPerMeeting = "Guesses per Meeting";
        }
    }


    protected override RoleModifier Modify(RoleModifier roleModifier) => roleModifier.VanillaRole(RoleTypes.Crewmate)
        .RoleFlags(RoleFlag.Hidden | RoleFlag.Unassignable | RoleFlag.CannotWinAlone | RoleFlag.DoNotTranslate | RoleFlag.DontRegisterOptions);
}