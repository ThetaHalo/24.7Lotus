using TOHTOR.API;
using TOHTOR.Extensions;
using TOHTOR.Roles.Internals;
using TOHTOR.Roles.Internals.Attributes;
using UnityEngine;

namespace TOHTOR.Roles.Subroles;

public class Bewilder: Subrole
{
    [RoleAction(RoleActionType.MyDeath)]
    private void BaitDies(PlayerControl killer)
    {
        CustomRole role = killer.GetCustomRole();
        role.AddOverride(new GameOptionOverride(Override.ImpostorLightMod, AUSettings.CrewLightMod()));
    }

    public override string Identifier() => "★";

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier).RoleColor(new Color(0.42f, 0.28f, 0.2f));
}