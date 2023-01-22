using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TownOfHost.Extensions;
using TownOfHost.GUI;
using TownOfHost.Options;
using VentLib.Utilities;

namespace TownOfHost.Roles;

public class Grenadier : Impostor
{
    [DynElement(UI.Cooldown)]
    private Cooldown blindCooldown;
    private float blindDuration;
    private float blindDistance;
    private bool canVent;
    private bool canBlindAllies;
    private int grenadeAmount;
    private int grenadesLeft;

    [RoleAction(RoleActionType.AttemptKill)]
    public new bool TryKill(PlayerControl target) => base.TryKill(target);

    [RoleAction(RoleActionType.OnPet)]
    private void GrenadierBlind()
    {
        if (blindCooldown.NotReady() || grenadesLeft <= 0) return;

        GameOptionOverride[] overrides = { new(Override.CrewLightMod, 0f), new(Override.ImpostorLightMod, 0f) };
        List<PlayerControl> playersInDistance = blindDistance > 0
            ? RoleUtils.GetPlayersWithinDistance(MyPlayer, blindDistance).ToList()
            : MyPlayer.GetPlayersInAbilityRangeSorted();

        playersInDistance.Where(p => canBlindAllies || !p.GetCustomRole().IsAllied(MyPlayer))
            .Do(p =>
            {
                p.GetCustomRole().SyncOptions(overrides);
                Async.Schedule(() => p.GetCustomRole().SyncOptions(), blindDuration);
            });

        blindCooldown.Start();
        grenadesLeft--;
    }

    [RoleAction(RoleActionType.RoundStart)]
    private void SetGrenadeAmount() => grenadesLeft = grenadeAmount;

    protected override SmartOptionBuilder RegisterOptions(SmartOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .AddSubOption(sub => sub
                .Name("Amount of Grenades")
                .Bind(v => grenadeAmount = (int)v)
                .AddIntRangeValues(1, 5, 1, 2, "s")
                .Build())
            .AddSubOption(sub => sub
                .Name("Blind Cooldown")
                .Bind(v => blindCooldown.Duration = (float)v)
                .AddFloatRangeValues(5f, 120f, 2.5f, 10, "s")
                .Build())
            .AddSubOption(sub => sub
                .Name("Blind Duration")
                .Bind(v => blindDuration = (float)v)
                .AddFloatRangeValues(5f, 60f, 2.5f, 4, "s")
                .Build())
            .AddSubOption(sub => sub
                .Name("Blind Distance")
                .Bind(v => blindDistance = (float)v)
                .AddValue(v => v.Text("Kill Distance").Value(-1f).Build())
                .AddFloatRangeValues(1.5f, 3f, 0.1f, 4)
                .Build())
            .AddSubOption(sub => sub
                .Name("Can Blind Allies")
                .Bind(v => canBlindAllies = (bool)v)
                .AddOnOffValues(false)
                .Build())
            .AddSubOption(sub => sub
                .Name("Can Vent")
                .Bind(v => canVent = (bool)v)
                .AddOnOffValues()
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .CanVent(canVent);
}