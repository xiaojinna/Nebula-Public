﻿using Nebula.Behaviour;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Marionette : DefinedSingleAbilityRoleTemplate<Marionette.Ability>, DefinedRole
{
    private Marionette() : base("marionette", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [PlaceCoolDownOption, SwapCoolDownOption, DecoyDurationOption, CanSeeDecoyInShadowOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);

        GameActionTypes.DecoyPlacementAction = new("marionette.placement", this, isEquippingAction: true);
    }


    static private FloatConfiguration PlaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.marionette.placeCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration SwapCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.marionette.swapCoolDown", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DecoyDurationOption = NebulaAPI.Configurations.Configuration("options.role.marionette.decoyDuration", (5f, 180f, 5f), 40f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanSeeDecoyInShadowOption = NebulaAPI.Configurations.Configuration("options.role.marionette.canSeeDecoyInShadow", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player);
    bool DefinedRole.IsJackalizable => true;

    static public Marionette MyRole = new Marionette();
    static private GameStatsEntry StatsDecoy = NebulaAPI.CreateStatsEntry("stats.marionette.decoy", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsSwap = NebulaAPI.CreateStatsEntry("stats.marionette.swap", GameStatsCategory.Roles, MyRole);

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class Decoy : NebulaSyncStandardObject
    {
        public static string MyTag = "Decoy";
        private static SpriteLoader decoySprite = SpriteLoader.FromResource("Nebula.Resources.Decoy.png", 150f);
        public Decoy(Vector2 pos,bool reverse) : base(pos,ZOption.Just,CanSeeDecoyInShadowOption, decoySprite.GetSprite()) {
            MyRenderer.flipX = reverse;
            MyBehaviour = MyRenderer.gameObject.AddComponent<EmptyBehaviour>();
        }

        public bool Flipped { get => MyRenderer.flipX; set => MyRenderer.flipX = value; }
        public EmptyBehaviour MyBehaviour = null!;

        static Decoy()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new Decoy(new Vector2(args[0], args[1]), args[2] < 0f));
        }
    }

    public class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private ModAbilityButton? placeButton = null;
        private ModAbilityButton? destroyButton = null;
        private ModAbilityButton? swapButton = null;
        private ModAbilityButton? monitorButton = null;

        static private Image placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyButton.png", 115f);
        static private Image destroyButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyDestroyButton.png", 115f);
        static private Image swapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoySwapButton.png", 115f);
        static private Image monitorButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DecoyMonitorButton.png", 115f);
        static private Image decoyArrowSprite = SpriteLoader.FromResource("Nebula.Resources.DecoyArrow.png", 180f);

        public Decoy? MyDecoy = null;

        AchievementToken<(bool isCleared, bool triggered)>? acTokenAnother = null;
        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<(bool cleared, float swapTime)>? acTokenCommon2 = null;
        AchievementToken<(bool cleared, float killTime)>? acTokenChallenge = null;
        Arrow? decoyArrow = null;

        //デコイの撤去
        void IGameOperator.OnReleased()
        {
            if (MyDecoy != null) NebulaSyncObject.RpcDestroy(MyDecoy!.ObjectId);
            MyDecoy = null;
        }

        public Ability(GamePlayer player) : base(player)
        {
            if (AmOwner)
            {
                decoyArrow = Bind(new Arrow(decoyArrowSprite.GetSprite(), false, true) { IsAffectedByComms = false, FixedAngle = true });
                decoyArrow.IsActive = false;

                acTokenAnother = AbstractAchievement.GenerateSimpleTriggerToken("marionette.another1");
                acTokenCommon2 = new("marionette.common2", (false,-100f),(val,_) => val.cleared);
                acTokenChallenge = new("marionette.challenge", (false,-100f),(val,_)=>val.cleared);

                placeButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability, "marionette.place");
                placeButton.SetSprite(placeButtonSprite.GetSprite());
                placeButton.Availability = (button) => MyPlayer.CanMove;
                placeButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy == null;
                placeButton.CoolDownTimer = Bind(new Timer(PlaceCoolDownOption).SetAsAbilityCoolDown().Start());
                placeButton.OnClick = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.DecoyPlacementAction);

                        MyDecoy = (NebulaSyncObject.RpcInstantiate(Decoy.MyTag, [
                        PlayerControl.LocalPlayer.transform.localPosition.x,
                        PlayerControl.LocalPlayer.transform.localPosition.y,
                        PlayerControl.LocalPlayer.cosmetics.FlipX ? -1f : 1f 
                        ]).SyncObject as Decoy);

                        destroyButton!.ActivateEffect();
                        destroyButton.EffectTimer?.Start();
                    });
                    placeButton.StartCoolDown();
                    swapButton?.StartCoolDown();
                    acTokenCommon ??= new("marionette.common1");
                    StatsDecoy.Progress();
                };
                placeButton.SetLabel("place");

                destroyButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability, "marionette.destroy");
                destroyButton.SetSprite(destroyButtonSprite.GetSprite());
                destroyButton.Availability = (button) => MyPlayer.CanMove;
                destroyButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy != null;
                destroyButton.EffectTimer = Bind(new Timer(DecoyDurationOption));
                destroyButton.OnClick = (button) =>
                {
                    destroyButton.InactivateEffect();
                };
                destroyButton.OnEffectEnd = (button) =>
                {
                    if (MyDecoy != null) NebulaSyncObject.RpcDestroy(MyDecoy!.ObjectId);
                    MyDecoy = null;

                    placeButton.StartCoolDown();
                };
                destroyButton.SetLabel("destroy");

                swapButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility, "marionette.swap").SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction, "marionette.switch");
                swapButton.SetSprite(swapButtonSprite.GetSprite());
                swapButton.Availability = (button) => (MyPlayer.CanMove || HudManager.Instance.PlayerCam.Target == MyDecoy?.MyBehaviour) && (!MyPlayer.VanillaPlayer.inVent && !MyPlayer.VanillaPlayer.onLadder && !MyPlayer.VanillaPlayer.inMovingPlat);
                swapButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy != null;
                swapButton.CoolDownTimer = Bind(new Timer(SwapCoolDownOption));
                swapButton.OnClick = (button) =>
                {
                    DecoySwap.Invoke((MyPlayer.PlayerId, MyDecoy!.ObjectId));
                    button.StartCoolDown();
                    AmongUsUtil.SetCamTarget();

                    float currentTime = NebulaGameManager.Instance!.CurrentTime;
                    acTokenCommon2.Value.cleared |= currentTime - acTokenCommon2.Value.swapTime < 10f;
                    acTokenCommon2.Value.swapTime = currentTime;
                    acTokenAnother!.Value.triggered = true;
                    if (currentTime - acTokenChallenge.Value.killTime < 1f && MyPlayer.VanillaPlayer.GetTruePosition().Distance(MyDecoy!.Position) > 30f)
                        acTokenChallenge.Value.cleared = true;

                    StatsSwap.Progress();
                };
                swapButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        swapButton.ResetKeyBind();
                        monitorButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility, "marionette.monitor");
                        monitorButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction, "marionette.switch");
                    });
                };
                swapButton.SetLabel("swap");

                monitorButton = Bind(new ModAbilityButton());
                monitorButton.SetSprite(monitorButtonSprite.GetSprite());
                monitorButton.Availability = (button) => true;
                monitorButton.Visibility = (button) => !MyPlayer.IsDead && MyDecoy != null;
                monitorButton.OnClick = (button) =>
                {
                    AmongUsUtil.ToggleCamTarget(MyDecoy!.MyBehaviour, null);
                };
                monitorButton.OnSubAction = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        monitorButton.ResetKeyBind();
                        swapButton!.KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility, "marionette.swap");
                        swapButton!.SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction, "marionette.switch");
                    });
                };
                monitorButton.SetLabel("monitor");
            }
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            if(AmOwner && decoyArrow != null)
            {
                if (MyDecoy == null)
                    decoyArrow.IsActive = false;
                else
                {
                    decoyArrow.IsActive = true;
                    decoyArrow.TargetPos = MyDecoy.Position;
                }
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
                if (MyDecoy != null) NebulaSyncObject.RpcDestroy(MyDecoy!.ObjectId);
                MyDecoy = null;

                monitorButton?.DoSubClick();
        }

        [OnlyMyPlayer]
        [Local]
        void OnDead(PlayerDieEvent ev)
        {
            if (acTokenAnother != null && (MyPlayer.PlayerState == PlayerState.Guessed || MyPlayer.PlayerState == PlayerState.Exiled)) acTokenAnother.Value.isCleared |= acTokenAnother.Value.triggered;
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenAnother != null) acTokenAnother.Value.triggered = false;
        }

        [OnlyMyPlayer]
        [Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (AmOwner && acTokenChallenge != null) acTokenChallenge.Value.killTime = NebulaGameManager.Instance!.CurrentTime;
        }

    }

    static private RemoteProcess<(byte playerId, int objId)> DecoySwap = new("DecoySwap",
        (message, _) => {
            var marionette = Helpers.GetPlayer(message.playerId);
            var decoy = NebulaSyncObject.GetObject<Decoy>(message.objId);
            if (marionette == null || decoy == null) return;
            var marionettePos = marionette.transform.localPosition;
            var marionetteFilp = marionette.cosmetics.FlipX;
            marionette.transform.localPosition = decoy.Position;
            marionette.cosmetics.SetFlipX(decoy.Flipped);
            decoy.Position = marionettePos;
            decoy.Flipped = marionetteFilp;
            marionette.NetTransform.Halt();
        }
        );
}

