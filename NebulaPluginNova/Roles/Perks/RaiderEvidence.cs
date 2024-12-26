﻿using Nebula.Roles.Impostor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game;

namespace Nebula.Roles.Perks;

internal class RaiderEvidence : PerkFunctionalInstance
{
    const float CoolDown = 10f;
    static PerkFunctionalDefinition Def = new("raiderEvidence", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("raiderEvidence", 3, 38, Virial.Color.ImpostorColor, Virial.Color.ImpostorColor).CooldownText("%CD%", CoolDown), (def, instance) => new RaiderEvidence(def, instance));

    public RaiderEvidence(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        cooldownTimer = new Timer(CoolDown).Start();
        PerkInstance.BindTimer(cooldownTimer);
    }

    private Timer cooldownTimer;
    private Raider.RaiderAxe? axe;

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing) return;
        if (!(axe?.CanThrow ?? false)) return;

        NebulaSyncObject.RpcInstantiate(Raider.RaiderAxe.MyGlobalFakeTag, [MyPlayer.PlayerId, axe!.Position.x, axe!.Position.y]);

        NebulaSyncObject.LocalDestroy(axe.ObjectId);
        axe = null;

        cooldownTimer.Start();

        SniperIcon.RegisterAchievementToken(MyPlayer);
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(cooldownTimer.IsProgressing ? Color.gray : Color.white);
        if(cooldownTimer.IsProgressing && axe != null)
        {
            NebulaSyncObject.LocalDestroy(axe.ObjectId);
            axe = null;
        }
        if(!cooldownTimer.IsProgressing && axe == null && MyPlayer.Role.Role != Impostor.Raider.MyRole)
        {
            axe = (NebulaSyncObject.LocalInstantiate(Raider.RaiderAxe.MyLocalFakeTag, [MyPlayer.PlayerId]).SyncObject as Raider.RaiderAxe)!;
        }
    }
    void OnMeetingEnd(TaskPhaseStartEvent ev)
    {
        cooldownTimer.Start();
    }

    protected override void OnReleased()
    {
        if(axe != null) NebulaSyncObject.LocalDestroy(axe.ObjectId);
    }
}
