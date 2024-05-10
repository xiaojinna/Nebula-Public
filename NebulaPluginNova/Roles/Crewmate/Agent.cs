﻿using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public class Agent : ConfigurableStandardRole
{
    static public Agent MyRole = new Agent();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "agent";
    public override Color RoleColor => new Color(166f / 255f, 183f / 255f, 144f / 255f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    private NebulaConfiguration NumOfExemptedTasksOption = null!;
    private NebulaConfiguration NumOfExtraTasksOption = null!;
    private NebulaConfiguration SuicideIfSomeoneElseCompletesTasksBeforeAgentOption = null!;
    private new VentConfiguration VentConfiguration = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        VentConfiguration = new(RoleConfig, (0, 16, 3), null, (2.5f, 30f, 10f));
        NumOfExemptedTasksOption = new(RoleConfig, "numOfExemptedTasks", null, 1, 8, 3, 3);
        NumOfExtraTasksOption = new(RoleConfig, "numOfExtraTasks", null, 0, 8, 3, 3);
        SuicideIfSomeoneElseCompletesTasksBeforeAgentOption = new(RoleConfig, "suicideIfSomeoneElseCompletesTasksBeforeAgent", null, false, false);
    }

    public class Instance : Crewmate.Instance, IBindPlayer
    {
        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AgentButton.png", 115f);
        public override AbstractRole Role => MyRole;
        public override bool CanUseVent => leftVent > 0;
        private int leftVent = MyRole.VentConfiguration.Uses;
        private Timer ventDuration = new Timer(MyRole.VentConfiguration.Duration);
        private TMPro.TextMeshPro UsesText = null!;

        public override Timer? VentDuration => ventDuration;
        public Instance(GamePlayer player, int[] argument) : base(player)
        {
            if (argument.Length >= 1) leftVent = argument[0];
        }

        public override int[]? GetRoleArgument() => new int[] { leftVent };

        [Local]
        void OnTaskUpdated(PlayerTaskUpdateEvent ev)
        {
            if (!MyPlayer.IsDead)
            {
                int tasks = AmongUsUtil.NumOfAllTasks;
                if (ev.Player.AmOwner) return;
                if (!ev.Player.IsDead && MyRole.SuicideIfSomeoneElseCompletesTasksBeforeAgentOption && ev.Player.Tasks.IsCrewmateTask && ev.Player.Tasks.TotalTasks >= tasks && ev.Player.Tasks.IsCompletedTotalTasks)
                {
                    MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Layoff);
                    new StaticAchievementToken("agent.another1");
                }
            }
        }

        public override void OnEnterVent(Vent vent)
        {
            if (AmOwner)
            {
                ventDuration.Start();

                leftVent--;
                UsesText.text = leftVent.ToString();
                if (leftVent <= 0) UsesText.transform.parent.gameObject.SetActive(false);
            }
        }

        public override void OnSetTaskLocal(ref List<GameData.TaskInfo> tasks, out int extraQuota)
        {
            extraQuota = 0;
            int extempts = MyRole.NumOfExemptedTasksOption;
            for (int i = 0; i < extempts; i++) tasks.RemoveAt(System.Random.Shared.Next(tasks.Count));
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                if (MyRole.NumOfExtraTasksOption.GetMappedInt() > 0)
                {
                    var taskButton = Bind(new ModAbilityButton()).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Ability));
                    taskButton.SetSprite(buttonSprite.GetSprite());
                    taskButton.Availability = (button) => MyPlayer.CanMove && MyPlayer.Tasks.IsCompletedCurrentTasks;
                    taskButton.Visibility = (button) => !MyPlayer.IsDead;
                    taskButton.OnClick = (button) =>
                    {
                        MyPlayer.Tasks.Unbox().GainExtraTasksAndRecompute(MyRole.NumOfExtraTasksOption, 0, 0, false);
                    };
                    taskButton.SetLabel("agent");
                }

                Bind(new GameObjectBinding(HudManager.Instance.ImpostorVentButton.ShowUsesIcon(3, out UsesText)));
                UsesText.text = leftVent.ToString();
            }
        }

        public override void OnGameEnd(EndState endState)
        {
            if (AmOwner)
            {
                if (endState.CheckWin(MyPlayer.PlayerId) && MyPlayer.Tasks.TotalTasks > 0 && MyRole.NumOfExemptedTasksOption <= 3)
                {
                    if (MyPlayer.Tasks.TotalCompleted - MyPlayer.Tasks.Quota > 0 && AmongUsUtil.NumOfAllTasks >= 8)
                        new StaticAchievementToken("agent.common1");

                    if(MyPlayer.Tasks.TotalCompleted - MyPlayer.Tasks.Quota >= 5)
                        new StaticAchievementToken("agent.challenge");
                }
            }
        }
    }
}

