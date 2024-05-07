﻿using System.Reflection;
using Virial.Attributes;

namespace Nebula.Roles;

[NebulaPreLoad(typeof(RemoteProcessBase),typeof(Team),typeof(NebulaAddon))]
public class Roles
{
    static public IReadOnlyList<AbstractRole> AllRoles { get; private set; } = null!;
    static public IReadOnlyList<AbstractModifier> AllModifiers { get; private set; } = null!;
    static public IReadOnlyList<AbstractGhostRole> AllGhostRoles { get; private set; } = null!;

    static public IEnumerable<IAssignableBase> AllAssignables()
    {
        foreach(var r in AllRoles) yield return r;
        foreach (var r in AllGhostRoles) yield return r;
        foreach (var m in AllModifiers) yield return m;
    }

    static public IEnumerable<IntroAssignableModifier> AllIntroAssignableModifiers()
    {
        foreach (var m in AllModifiers) if (m is IntroAssignableModifier iam) yield return iam;
    }

    static public IReadOnlyList<Team> AllTeams { get; private set; } = null!;

    static private List<AbstractRole>? allRoles = new();
    static private List<AbstractGhostRole>? allGhostRoles = new();
    static private List<AbstractModifier>? allModifiers = new();
    static private List<Team>? allTeams = new();

    static public void Register(AbstractRole role) {
        if(allRoles == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register role \"{role.LocalizedName}\".\nRole registration is only possible at load phase.");
        else
            allRoles?.Add(role);
    }
    static public void Register(AbstractGhostRole role)
    {
        if (allRoles == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register role \"{role.LocalizedName}\".\nRole registration is only possible at load phase.");
        else
            allGhostRoles?.Add(role);
    }
    static public void Register(AbstractModifier role)
    {
        if(allModifiers == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register modifier \"{role.LocalizedName}\".\nModifier registration is only possible at load phase.");
        else
            allModifiers?.Add(role);
    }
    static public void Register(Team team) {
        if(allTeams == null)
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Role, $"Failed to register team \"{team.TranslationKey}\".\nTeam registration is only possible at load phase.");
        else
            allTeams.Add(team);
    }


    static private void SetNebulaTeams()
    {
        //Set Up Team
        Virial.Assignable.NebulaTeams.CrewmateTeam = Crewmate.Crewmate.MyTeam;
        Virial.Assignable.NebulaTeams.ImpostorTeam = Impostor.Impostor.MyTeam;
        Virial.Assignable.NebulaTeams.ArsonistTeam = Neutral.Arsonist.MyTeam;
        Virial.Assignable.NebulaTeams.ChainShifterTeam = Neutral.ChainShifter.MyTeam;
        Virial.Assignable.NebulaTeams.JackalTeam = Neutral.Jackal.MyTeam;
        Virial.Assignable.NebulaTeams.JesterTeam = Neutral.Jester.MyTeam;
        Virial.Assignable.NebulaTeams.PaparazzoTeam = Neutral.Paparazzo.MyTeam;
        Virial.Assignable.NebulaTeams.VultureTeam = Neutral.Vulture.MyTeam;
    }

    static public IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Building Roles Database";
        yield return null;

        var iroleType = typeof(AbstractRole);
        var types = Assembly.GetAssembly(typeof(AbstractRole))?.GetTypes().Where((type) => type.IsAssignableTo(typeof(IAssignableBase)) || type.IsAssignableTo(typeof(PerkInstance)) || type.IsDefined(typeof(NebulaRoleHolder)));
        if (types == null) yield break;

        foreach (var type in types)
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

        SetNebulaTeams();

        allRoles!.Sort((role1, role2) => {
            int diff;
            
            diff = (int)role1.Category - (int)role2.Category;
            if (diff != 0) return diff;

            diff = (role1.IsDefaultRole ? -1 : 1) - (role2.IsDefaultRole ? -1 : 1);
            if (diff != 0) return diff;

            return role1.InternalName.CompareTo(role2.InternalName);
        });

        allGhostRoles!.Sort((role1, role2) => {
            int diff = (int)role1.Category - (int)role2.Category;
            if (diff != 0) return diff;
            return role1.InternalName.CompareTo(role2.InternalName);
        });


        allModifiers!.Sort((role1, role2) => {
            return role1.InternalName.CompareTo(role2.InternalName);
        });

        allTeams!.Sort((team1, team2) => team1.TranslationKey.CompareTo(team2.TranslationKey));

        for (int i = 0; i < allRoles!.Count; i++) allRoles![i].Id = i;
        for (int i = 0; i < allGhostRoles!.Count; i++) allGhostRoles![i].Id = i;
        for (int i = 0; i < allModifiers!.Count; i++) allModifiers![i].Id = i;

        AllRoles = allRoles!.AsReadOnly();
        AllGhostRoles = allGhostRoles!.AsReadOnly();
        AllModifiers = allModifiers!.AsReadOnly();
        AllTeams = allTeams!.AsReadOnly();

        foreach (var role in allRoles) role.Load();
        foreach (var role in allGhostRoles) role.Load();
        foreach (var modifier in allModifiers) modifier.Load();

        //Can Be Guessedのオプション
        foreach (var role in allRoles.Where(r => r.CanBeGuessDefault)) role.CanBeGuessOption = new NebulaConfiguration(null, "role." + role.LocalizedName + ".canBeGuess", null, true, true);
        

        allRoles = null;
        allGhostRoles = null;
        allModifiers = null;
        allTeams = null;
    }
}
