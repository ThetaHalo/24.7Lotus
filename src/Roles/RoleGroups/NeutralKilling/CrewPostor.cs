using System.Collections.Generic;
using System.Linq;
using TOHTOR.API;
using TOHTOR.API.Odyssey;
using TOHTOR.Extensions;
using TOHTOR.Factions;
using TOHTOR.Managers.History.Events;
using TOHTOR.Options;
using TOHTOR.Roles.Interactions;
using TOHTOR.Roles.Internals;
using TOHTOR.Roles.RoleGroups.Vanilla;
using UnityEngine;
using VentLib.Options.Game;
using VentLib.Utilities.Extensions;

namespace TOHTOR.Roles.RoleGroups.NeutralKilling;

public class CrewPostor : Crewmate
{
    private bool warpToTarget;
    private bool canKillAllied;

    protected override void OnTaskComplete()
    {
        if (MyPlayer.Data.IsDead) return;
        List<PlayerControl> inRangePlayers = RoleUtils.GetPlayersWithinDistance(MyPlayer, 999, true).Where(p => canKillAllied || p.Relationship(MyPlayer) is not Relation.FullAllies).ToList();
        if (inRangePlayers.Count == 0) return;
        PlayerControl target = inRangePlayers.GetRandom();
        var interaction = new RangedInteraction(new FatalIntent(!warpToTarget, () => new TaskDeathEvent(target, MyPlayer)), 0, this);

        bool death = MyPlayer.InteractWith(target, interaction) is InteractionResult.Proceed;
        Game.GameHistory.AddEvent(new TaskKillEvent(MyPlayer, target, death));
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        AddTaskOverrideOptions(base.RegisterOptions(optionStream)
            .Tab(DefaultTabs.ImpostorsTab)
            .SubOption(sub => sub.Name("Warp to Target")
                .AddOnOffValues()
                .BindBool(b => warpToTarget = b)
                .Build()))
            .SubOption(sub => sub.Name("Can Kill Allies")
                .AddOnOffValues(false)
                .BindBool(b => canKillAllied = b)
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(0.86f, 0.4f, 0f))
            .SpecialType(SpecialType.Madmate)
            .Faction(FactionInstances.Madmates);

    class TaskKillEvent : KillEvent, IRoleEvent
    {
        public TaskKillEvent(PlayerControl killer, PlayerControl victim, bool successful = true) : base(killer, victim, successful)
        {
        }

        public override string Message() => $"{Game.GetName(Player())} viciously completed his task and killed {Game.GetName(Target())}.";
    }

    class TaskDeathEvent : DeathEvent
    {
        public TaskDeathEvent(PlayerControl deadPlayer, PlayerControl? killer) : base(deadPlayer, killer)
        {
        }
    }
}