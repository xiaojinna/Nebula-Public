﻿using Nebula.Behaviour;
using Nebula.Game.Statistics;
using Virial.DI;
using Virial.Events.Player;
using static Nebula.Modules.HelpScreen;

namespace Nebula.Patches;


[HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
public static class HudManagerStartPatch
{
    static void Prefix(HudManager __instance)
    {
        NebulaGameManager.Instance?.Abandon();
    }
    static void Postfix(HudManager __instance)
    {
        DIManager.Instance.Instantiate<Virial.Game.Game>();
        
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Light(Dummy)", AmongUsUtil.GetShadowCollab().ShadowCamera.transform, new Vector3(0, 0, -1.5f), LayerExpansion.GetDrawShadowsLayer());
        renderer.sprite = VanillaAsset.FullScreenSprite;
        renderer.material.shader = NebulaAsset.StoreBackShader;
        renderer.color = Color.clear;
        
        __instance.TaskPanel.transform.localPosition = new(0, 0, 0);
    }
}


[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HudManagerUpdatePatch
{
    static void Postfix(HudManager __instance)
    {
        __instance.UpdateHudContent();
        NebulaGameManager.Instance?.OnUpdate();

        if (!TextField.AnyoneValid &&  NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Help).KeyDownForAction && !IntroCutscene.Instance && !Minigame.Instance && !ExileController.Instance)
        {
            HelpScreen.TryOpenHelpScreen(HelpTab.MyInfo);
        }
    }
}


[HarmonyPatch(typeof(HudManager), nameof(HudManager.CoShowIntro))]
public static class HudManagerCoStartGamePatch
{
    static bool Prefix(HudManager __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        IEnumerator GetEnumerator(){
            //UIを閉じる
            NebulaManager.Instance.CloseAllUI();

            while (!ShipStatus.Instance)
            {
                yield return null;
            }
            __instance.IsIntroDisplayed = true;
            DestroyableSingleton<HudManager>.Instance.FullScreen.transform.localPosition = new Vector3(0f, 0f, -250f);
            yield return DestroyableSingleton<HudManager>.Instance.ShowEmblem(true);
            IntroCutscene introCutscene = GameObject.Instantiate<IntroCutscene>(__instance.IntroPrefab, __instance.transform);
            yield return introCutscene.CoBegin();

            yield return ModPreSpawnInPatch.ModPreSpawnIn(__instance.transform, GameStatistics.EventVariation.GameStart, EventDetail.GameStart);

            PlayerControl.LocalPlayer.killTimer = 10f;
            if (GeneralConfigurations.EmergencyCooldownAtGameStart) AmongUsUtil.SetEmergencyCoolDown(10f, false, false);

            HudManager.Instance.KillButton.SetCoolDown(10f, AmongUsUtil.VanillaKillCoolDown);

            ShipStatus.Instance.Systems[SystemTypes.Sabotage].Cast<SabotageSystemType>().SetInitialSabotageCooldown();
            if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out var door)) door.TryCast<IDoorSystem>()?.SetInitialSabotageCooldown();

            PlayerControl.LocalPlayer.AdjustLighting();
            yield return __instance.CoFadeFullScreen(Color.black, Color.clear, 0.2f, false);
            __instance.FullScreen.transform.localPosition = new Vector3(0f, 0f, -500f);
            __instance.IsIntroDisplayed = false;
            __instance.CrewmatesKilled.gameObject.SetActive(GameManager.Instance.ShowCrewmatesKilled());
            GameManager.Instance.StartGame();
        }
        __result = GetEnumerator().WrapToIl2Cpp();

        return false;
    }

}

[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
class TaskTextPatch
{
    public static void Postfix(TaskPanelBehaviour __instance)
    {
        try
        {
            __instance.taskText.text = __instance.taskText.text + GameOperatorManager.Instance?.Run(new PlayerTaskTextLocalEvent(NebulaGameManager.Instance!.LocalPlayerInfo)).Text;
        }
        catch { }
    }
}