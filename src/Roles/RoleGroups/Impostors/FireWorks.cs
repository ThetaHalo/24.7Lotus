using System.Collections.Generic;
using System.Linq;
using Lotus.API.Player;
using Lotus.Extensions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Options;
using Lotus.Roles.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities.Extensions;
using Lotus.Roles.Internals;

namespace Lotus.Roles.RoleGroups.Impostors;

public class FireWorks : Shapeshifter
{
    [NewOnSetup] private List<Vector2> fireWorkLocations = null!;
    private int maxFireworks;
    private int totalFireworks;
    private int plantedFireworks;
    private bool detonateWhenLastImp;

    private FixedUpdateLock fixedUpdateLock = new(0.25f);

    private int impostorCount;

    [UIComponent(UI.Counter)]
    public string FireworkCounter() => totalFireworks >= 0 ? RoleUtils.Counter(totalFireworks, maxFireworks, ModConstants.Palette.MadmateColor) : RoleUtils.Counter(plantedFireworks, color: ModConstants.Palette.MadmateColor);

    [UIComponent(UI.Text)]
    public string DetonateText() => detonateWhenLastImp && impostorCount == 1 && plantedFireworks != 0 ? Translations.AbleToDetonateText : "";

    protected override void PostSetup()
    {
        ShapeshiftDuration = 5f;
        maxFireworks = totalFireworks;
    }

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target) => base.TryKill(target);

    [RoleAction(LotusActionType.FixedUpdate)]
    public void FireworkImpostorCounter()
    {
        if (!fixedUpdateLock.AcquireLock() || !detonateWhenLastImp) return;
        impostorCount = GetAliveImpostors();
    }

    [RoleAction(LotusActionType.Unshapeshift)]
    public void DoFireworkAbility(ActionHandle handle)
    {
        // handle.Cancel();
        if (impostorCount == 1 && plantedFireworks != 0 && detonateWhenLastImp) DetonateFireworks();
        else if (totalFireworks is -1 or >= 1)
        {
            fireWorkLocations.Add(MyPlayer.GetTruePosition());
            plantedFireworks++;
            if (totalFireworks != -1) totalFireworks--;
        }
    }

    [RoleAction(LotusActionType.OnPet)]
    public void DetonateFromPet()
    {
        if (!detonateWhenLastImp) DetonateFireworks();
    }

    public void DetonateFireworks()
    {
        plantedFireworks = 0;
        fireWorkLocations.SelectMany(fl => RoleUtils.GetPlayersWithinDistance(fl, 1.8f)).DistinctBy(p => p.PlayerId).ForEach(p =>
        {
            BombedEvent bombedEvent = new(p, MyPlayer);
            MyPlayer.InteractWith(p, new LotusInteraction(new FatalIntent(true, () => bombedEvent), this));
        });
    }

    private int GetAliveImpostors() => Players.GetPlayers(PlayerFilter.Alive | PlayerFilter.Impostor).Count();

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.KeyName("Plant Firework Cooldown", Translations.Options.PlantFireworkCooldown)
                .AddFloatRange(2.5f, 120f, 2.5f, 19, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(f => ShapeshiftCooldown = f)
                .Build())
            .SubOption(sub => sub.KeyName("Total Firework Charges", Translations.Options.TotalFireworks)
                .Value(v => v.Text(ModConstants.Infinity).Color(ModConstants.Palette.InfinityColor).Value(-1).Build())
                .AddIntRange(1, 20, 1, 0)
                .BindInt(i => totalFireworks = i)
                .Build())
            .SubOption(sub => sub.KeyName("Detonate Only When Last Impostor", TranslationUtil.Colorize(Translations.Options.AbleToDetonateEarly, RoleColor))
                .BindBool(b => detonateWhenLastImp = b)
                .AddOnOffValues()
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet | RoleAbilityFlag.UsesUnshiftTrigger);

    [Localized(nameof(FireWorks))]
    private static class Translations
    {
        [Localized(nameof(AbleToDetonateText))]
        public static string AbleToDetonateText = "Detonations Ready!";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(PlantFireworkCooldown))]
            public static string PlantFireworkCooldown = "Plant Firework Cooldown";

            [Localized(nameof(TotalFireworks))]
            public static string TotalFireworks = "Total Firework Charges";

            [Localized(nameof(AbleToDetonateEarly))]
            public static string AbleToDetonateEarly = "Detonate Only When Last Impostor::0";
        }
    }
}