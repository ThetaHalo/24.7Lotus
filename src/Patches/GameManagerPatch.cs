using HarmonyLib;
using Hazel;

namespace Lotus.Patches;

[HarmonyPatch(typeof(GameManager), nameof(GameManager.Serialize))]
class GameManagerSerializeFix
{
    public static bool Prefix(GameManager __instance, [HarmonyArgument(0)] MessageWriter writer, [HarmonyArgument(1)] bool initialState, ref bool __result)
    {
        bool flag = false;
        for (int index = 0; index < __instance.LogicComponents.Count; ++index)
        {
            GameLogicComponent logicComponent = __instance.LogicComponents[index];
            if (initialState || logicComponent.IsDirty)
            {
                flag = true;
                writer.StartMessage((byte)index);
                logicComponent.Serialize(writer, initialState);
                writer.EndMessage();
                logicComponent.ClearDirtyFlag();
            }
        }
        __instance.ClearDirtyBits();
        __result = flag;
        return false;
    }
}
[HarmonyPatch(typeof(LogicOptions), nameof(LogicOptions.Serialize))]
class LogicOptionsSerializePatch
{
    public static bool Prefix(LogicOptions __instance, ref bool __result, MessageWriter writer, bool initialState)
    {
        // 初回以外はブロックし、CustomSyncSettingsでのみ同期する
        if (initialState) return true;
        __result = false;
        return false;

    }
}