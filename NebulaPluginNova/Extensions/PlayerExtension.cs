﻿using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Behaviour;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Extensions;

[NebulaRPCHolder]
public static class PlayerExtension
{

    public static IEnumerator CoDive(this PlayerControl player)
    {

        player.MyPhysics.body.velocity = Vector2.zero;
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = true;
        player.cosmetics.skin.SetEnterVent(player.cosmetics.FlipX);
        player.moveable = false;

        yield return player.MyPhysics.Animations.CoPlayEnterVentAnimation(0);

        player.MyPhysics.myPlayer.Visible = false;
        player.cosmetics.skin.SetIdle(player.cosmetics.FlipX);
        player.MyPhysics.Animations.PlayIdleAnimation();
        player.moveable = true;

        player.currentRoleAnimations.ForEach((Action<RoleEffectAnimation>)((an) => an.ToggleRenderer(false)));
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = false;
    }

    public static IEnumerator CoGush(this PlayerControl player)
    {

        player.MyPhysics.body.velocity = Vector2.zero;
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = true;
        player.moveable = false;
        player.MyPhysics.myPlayer.Visible = true;
        player.cosmetics.AnimateSkinExitVent();

        yield return player.MyPhysics.Animations.CoPlayExitVentAnimation();

        player.cosmetics.AnimateSkinIdle();
        player.MyPhysics.Animations.PlayIdleAnimation();
        player.moveable = true;
        player.currentRoleAnimations.ForEach((Action<RoleEffectAnimation>)((an) => an.ToggleRenderer(true)));
        if (player.AmOwner) player.MyPhysics.inputHandler.enabled = false;
    }

    public static void HaltSmoothly(this CustomNetworkTransform netTransform)
    {
        ushort minSid = (ushort)(netTransform.lastSequenceId + 1);
        netTransform.SnapToSmoothly(netTransform.transform.position);
    }

    public static void SnapToSmoothly(this CustomNetworkTransform netTransform, Vector2 position)
    {
        //netTransform.ClearPositionQueues();
        
        Transform transform = netTransform.transform;
        netTransform.body.position = position;
        transform.position = position;
        netTransform.body.velocity = Vector2.zero;

        netTransform.sendQueue.Enqueue(position);
        netTransform.SetDirtyBit(2U);
    }

    public static void StopAllAnimations(this CosmeticsLayer layer)
    {
        try
        {
            if (layer.skin.animator) layer.skin.animator.Stop();
            if (layer.currentPet.animator) layer.currentPet.animator.Stop();
        }
        catch { }
    }

    static RemoteProcess<(byte killerId, byte targetId, int stateId, int recordId, bool blink,bool showOverlay, bool assignGhostRole)> RpcKill = new(
        "Kill",
       (message, _) =>
       {
           var recordTag = TranslatableTag.ValueOf(message.recordId);
           if (recordTag != null)
               NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, message.killerId == byte.MaxValue ? null : message.killerId, 1 << message.targetId) { RelatedTag = recordTag });

           var killer = Helpers.GetPlayer(message.killerId == byte.MaxValue ? message.targetId : message.killerId);
           var target = Helpers.GetPlayer(message.targetId);

           if (target == null) return;

           // MurderPlayer ここから

           if (killer && killer!.AmOwner)
           {
               if (Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(killer.KillSfx, false, 0.8f, null);
               killer.SetKillTimer(AmongUsUtil.VanillaKillCoolDown);
           }

           target.gameObject.layer = LayerMask.NameToLayer("Ghost");

           if (target.AmOwner)
           {
               StatsManager.Instance.IncrementStat(StringNames.StatsTimesMurdered);
               if (Minigame.Instance)
               {
                   try
                   {
                       Minigame.Instance.Close();
                       Minigame.Instance.Close();
                   }
                   catch
                   {
                   }
               }
               if (message.showOverlay) DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(killer ? killer!.Data : null, target.Data);
               target.cosmetics.SetNameMask(false);
               target.RpcSetScanner(false);
           }
           if (killer) killer!.MyPhysics.StartCoroutine(killer.KillAnimations[System.Random.Shared.Next(killer.KillAnimations.Count)].CoPerformModKill(killer, target, message.blink).WrapToIl2Cpp());

           // MurderPlayer ここまで


           var targetInfo = target.GetModInfo();

           var killerInfo = killer?.GetModInfo();

           if (targetInfo != null)
           {
               targetInfo.Unbox().DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
               targetInfo.Unbox().MyKiller = killerInfo;
               targetInfo.Unbox().MyState = TranslatableTag.ValueOf(message.stateId);
               
               if (targetInfo.AmOwner && NebulaAchievementManager.GetRecord("death." + targetInfo!.PlayerState.TranslationKey, out var rec)) new StaticAchievementToken(rec);
               if ((killerInfo?.AmOwner ?? false) && NebulaAchievementManager.GetRecord("kill." + targetInfo!.PlayerState.TranslationKey, out var recKill)) new StaticAchievementToken(recKill);

               targetInfo.VanillaPlayer.Data.IsDead = true;

               //1ずつ加算するのでこれで十分
               if (targetInfo.AmOwner && (NebulaGameManager.Instance?.AllPlayerInfo().Count(p => p.IsDead) ?? 0) == 1)
                   new StaticAchievementToken("firstKill");

               if (message.assignGhostRole && targetInfo.AmOwner) NebulaGameManager.RpcTryAssignGhostRole.Invoke(targetInfo);

           }

           //Entityイベント発火
           if (targetInfo != null)
           {
               if (killerInfo != null)
               {
                   GameOperatorManager.Instance?.Run(new PlayerKillPlayerEvent(killerInfo, targetInfo), true);
                   GameOperatorManager.Instance?.Run(new PlayerMurderedEvent(targetInfo, killerInfo), true);
               }
               else
                   GameOperatorManager.Instance?.Run(new PlayerDieEvent(targetInfo));
           }
       }
       );

    static RemoteProcess<(byte killerId, byte targetId, int stateId, int recordId, bool showOverlay, bool playSE, bool assignGhostRole)> RpcMeetingKill = new(
        "NonPhysicalKill",
       (message, _) =>
       {
           var recordTag = TranslatableTag.ValueOf(message.recordId);
           if (recordTag != null)
               NebulaGameManager.Instance?.GameStatistics.RecordEvent(new GameStatistics.Event(GameStatistics.EventVariation.Kill, message.killerId, 1 << message.targetId) { RelatedTag = recordTag });

           var killer = Helpers.GetPlayer(message.killerId);
           var target = Helpers.GetPlayer(message.targetId);

           if (target == null) return;

           if (!target.AmOwner && message.playSE && Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(target.KillSfx, false, 0.8f, null);

           target.Die(DeathReason.Exile, false);

           if (target.AmOwner)
           {
               if(message.showOverlay) DestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(killer ? killer!.Data : null, target.Data);

               NebulaGameManager.Instance!.ChangeToSpectator();
           }


           if (MeetingHud.Instance != null) MeetingHud.Instance.ResetPlayerState();
           


           var targetInfo = target.GetModInfo();
           var killerInfo = killer.GetModInfo();

           if (targetInfo != null)
           {
               targetInfo.Unbox().DeathTimeStamp = NebulaGameManager.Instance!.CurrentTime;
               targetInfo.Unbox().MyKiller = killerInfo;
               targetInfo.Unbox().MyState = TranslatableTag.ValueOf(message.stateId);
               if (targetInfo.AmOwner && NebulaAchievementManager.GetRecord("death." + targetInfo!.PlayerState.TranslationKey, out var rec)) new StaticAchievementToken(rec);
               if ((killerInfo?.AmOwner ?? false) && NebulaAchievementManager.GetRecord("kill." + targetInfo!.PlayerState.TranslationKey, out var recKill)) new StaticAchievementToken(recKill);


               //Entityイベント発火
               if (killerInfo != null)
               {
                   GameOperatorManager.Instance?.Run(new PlayerKillPlayerEvent(killerInfo, targetInfo), true);
                   GameOperatorManager.Instance?.Run(new PlayerMurderedEvent(targetInfo, killerInfo), true);
               }
               else
                   GameOperatorManager.Instance?.Run(new PlayerDieEvent(targetInfo));

               if (message.assignGhostRole && targetInfo.AmOwner) NebulaGameManager.RpcTryAssignGhostRole.Invoke(targetInfo);
           }

           if (MeetingHud.Instance)
           {
               IEnumerator CoGainDiscussionTime()
               {
                   for(int i = 0; i < 10; i++)
                   {
                       MeetingHudExtension.VotingTimer += 1f;
                       MeetingHud.Instance!.lastSecond = 11;
                       yield return new WaitForSeconds(0.1f);
                   }
               }
               NebulaManager.Instance!.StartCoroutine(CoGainDiscussionTime().WrapToIl2Cpp());
           }
       }
       );

    static RemoteProcess<(byte exiledId, byte sourceId, CommunicableTextTag stateId, CommunicableTextTag recordId)> RpcMarkAsExtraVictim = new(
        "MarkAsExtraVictim",
        (message, _) => MeetingHudExtension.ExtraVictims.Add(message)
        );

    static public KillResult ModFlexibleKill(this PlayerControl killer, PlayerControl target, bool showBlink, CommunicableTextTag playerState, CommunicableTextTag? recordState, bool showOverlay, bool assignGhostRole = true)
    {
        bool isMeetingKill = MeetingHud.Instance;
        if (CheckKill(null, target, playerState, recordState, isMeetingKill, out var result))
        {
            if (MeetingHud.Instance)
                RpcMeetingKill.Invoke((killer.PlayerId, target.PlayerId, playerState.Id, recordState?.Id ?? int.MaxValue, showOverlay, false, assignGhostRole));
            else
                RpcKill.Invoke((killer.PlayerId, target.PlayerId, playerState.Id, recordState?.Id ?? int.MaxValue, showBlink, showOverlay, assignGhostRole));
        }
        return result;

    }

    static private bool CheckKill(PlayerControl? killer, PlayerControl target, CommunicableTextTag playerState, CommunicableTextTag? recordState, bool isMeetingKill, out KillResult result)
    {
        var targetInfo = target.GetModInfo()!;
        var killerInfo = killer?.GetModInfo() ?? targetInfo;
        var localResult = KillResult.Kill;
        targetInfo.Unbox().AssignableAction(r => { if (localResult == KillResult.Kill) localResult = r.Unbox().CheckKill(killerInfo, playerState, recordState, isMeetingKill); });
        result = localResult;
           
        if (result != KillResult.Kill) RpcOnGuard.Invoke((killerInfo.PlayerId, targetInfo.PlayerId, result == KillResult.ObviousGuard));



        return result == KillResult.Kill;
    }

    static public KillResult ModSuicide(this PlayerControl target, bool showBlink, CommunicableTextTag playerState, CommunicableTextTag? recordState, bool showOverlay = true, bool assignGhostRole = true)
    => ModFlexibleKill(target,target,showBlink, playerState, recordState, showOverlay, true);
    
    static public KillResult ModKill(this PlayerControl killer, PlayerControl target, bool showBlink, CommunicableTextTag playerState, CommunicableTextTag? recordState, bool showOverlay = true, bool tryAssignGhostRole = true)
    {
        if (CheckKill(killer, target, playerState, recordState, false, out var result))
            RpcKill.Invoke((killer.PlayerId, target.PlayerId, playerState.Id, recordState?.Id ?? int.MaxValue, showBlink,showOverlay, tryAssignGhostRole));
        return result;
    }

    static public KillResult ModMeetingKill(this PlayerControl killer, PlayerControl target, bool showOverlay, CommunicableTextTag playerState, CommunicableTextTag? recordState, bool playSE = true)
    {
        if (CheckKill(killer, target, playerState, recordState, true, out var result))
            RpcMeetingKill.Invoke((killer.PlayerId, target.PlayerId, playerState.Id, recordState?.Id ?? int.MaxValue, showOverlay,playSE, true));
        return result;
    }

    static public void ModMarkAsExtraVictim(this PlayerControl exiled,PlayerControl? source, CommunicableTextTag playerState, CommunicableTextTag recordState)
    {
        RpcMarkAsExtraVictim.Invoke((exiled.PlayerId, source?.PlayerId ?? byte.MaxValue, playerState, recordState));

    }

    static public void ModDive(this PlayerControl player, bool isDive = true)
    {
        RpcDive.Invoke((player.PlayerId,isDive));
    }

    static RemoteProcess<(byte sourceId, byte targetId, Vector2 revivePos, bool cleanDeadBody,bool recordEvent)> RpcRivive = new(
        "Revive",
        (message, _) =>
        {
            var player = Helpers.GetPlayer(message.targetId);
            if (!player) return;

            player!.Revive();
            player.NetTransform.SnapTo(message.revivePos);
            player.GetModInfo()!.Unbox().MyState = PlayerState.Revived;

            if (message.cleanDeadBody) foreach (var d in Helpers.AllDeadBodies()) if (d.ParentId == player.PlayerId) GameObject.Destroy(d.gameObject);

            if(message.recordEvent)NebulaGameManager.Instance?.GameStatistics.RecordEvent(new(GameStatistics.EventVariation.Revive, message.sourceId != byte.MaxValue ? message.sourceId : null, 1 << message.targetId) { RelatedTag = EventDetail.Revive });
        }
        );

    static RemoteProcess<(byte playerId, bool isDive)> RpcDive = new(
        "Dive",
        (message, _) =>
        {
            var player = Helpers.GetPlayer(message.playerId);
            if (!player) return;
            player?.StartCoroutine(message.isDive ? player.CoDive() : player.CoGush());
        }
        );

    static RemoteProcess<(byte killerId, byte targetId, bool targetCanSeeGuard)> RpcOnGuard = new(
        "Guard",
        (message, _) =>
        {
            var killer = NebulaGameManager.Instance?.GetPlayer(message.killerId)!;

            GameOperatorManager.Instance?.Run(new PlayerGuardEvent(NebulaGameManager.Instance?.GetPlayer(message.targetId), killer));

            if (message.killerId == PlayerControl.LocalPlayer.PlayerId || (message.targetCanSeeGuard && message.targetId == PlayerControl.LocalPlayer.PlayerId))
            {
                Helpers.GetPlayer(message.targetId)?.ShowFailedMurder();
                PlayerControl.LocalPlayer.SetKillTimer(AmongUsUtil.VanillaKillCoolDown);
            }
        }
        );

    static public void ModRevive(this PlayerControl player, Vector2 pos, bool cleanDeadBody,bool recordEvent)
    {
        RpcRivive.Invoke((byte.MaxValue, player.PlayerId, pos, cleanDeadBody,recordEvent));
    }

    static public void ModRevive(this PlayerControl player, PlayerControl? healer, Vector2 pos, bool cleanDeadBody, bool recordEvent = true)
    {
        RpcRivive.Invoke((healer?.PlayerId ?? byte.MaxValue, player.PlayerId, pos, cleanDeadBody, recordEvent));
    }

    static public ModTitleShower GetTitleShower(this PlayerControl player)
    {
        if (player.TryGetComponent<ModTitleShower>(out var result))
            return result;
        else
        {
            return player.gameObject.AddComponent<ModTitleShower>();
        }
    }
}
