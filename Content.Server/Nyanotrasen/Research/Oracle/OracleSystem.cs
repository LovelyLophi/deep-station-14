using System.Linq;
using Content.Server.Botany;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Psionics;
using Content.Shared.Psionics.Abilities;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Psionics.Glimmer;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Research.Oracle;

public sealed class OracleSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var oracle in EntityQuery<OracleComponent>())
        {
            oracle.Accumulator += frameTime;
            oracle.BarkAccumulator += frameTime;
            oracle.RejectAccumulator += frameTime;
            if (oracle.BarkAccumulator >= oracle.BarkTime.TotalSeconds)
            {
                oracle.BarkAccumulator = 0;
                var message = Loc.GetString(_random.Pick(oracle.DemandMessages), ("item", oracle.DesiredPrototype.Name))
                    .ToUpper();
                _chat.TrySendInGameICMessage(oracle.Owner, message, InGameICChatType.Speak, false);
            }

            if (oracle.Accumulator >= oracle.ResetTime.TotalSeconds)
            {
                oracle.LastDesiredPrototype = oracle.DesiredPrototype;
                NextItem(oracle);
            }
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OracleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<OracleComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<OracleComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInit(EntityUid uid, OracleComponent component, ComponentInit args)
    {
        NextItem(component);
    }

    private void OnInteractHand(EntityUid uid, OracleComponent component, InteractHandEvent args)
    {
        if (!HasComp<PotentialPsionicComponent>(args.User) || HasComp<PsionicInsulationComponent>(args.User))
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var message = Loc.GetString("oracle-current-item", ("item", component.DesiredPrototype.Name));

        var messageWrap = Loc.GetString("chat-manager-send-telepathic-chat-wrap-message",
            ("telepathicChannelName", Loc.GetString("chat-manager-telepathic-channel-name")), ("message", message));

        _chatManager.ChatMessageToOne(ChatChannel.Telepathic,
            message, messageWrap, uid, false, actor.PlayerSession.ConnectedClient, Color.PaleVioletRed);

        if (component.LastDesiredPrototype != null)
        {
            var message2 = Loc.GetString("oracle-previous-item", ("item", component.LastDesiredPrototype.Name));
            var messageWrap2 = Loc.GetString("chat-manager-send-telepathic-chat-wrap-message",
                ("telepathicChannelName", Loc.GetString("chat-manager-telepathic-channel-name")),
                ("message", message2));

            _chatManager.ChatMessageToOne(ChatChannel.Telepathic,
                message2, messageWrap2, uid, false, actor.PlayerSession.ConnectedClient, Color.PaleVioletRed);
        }
    }

    private void OnInteractUsing(EntityUid uid, OracleComponent component, InteractUsingEvent args)
    {
        if (HasComp<MobStateComponent>(args.Used))
            return;

        if (!TryComp<MetaDataComponent>(args.Used, out var meta))
            return;

        if (meta.EntityPrototype == null)
            return;

        var validItem = CheckValidity(meta.EntityPrototype, component.DesiredPrototype);

        var nextItem = true;

        if (component.LastDesiredPrototype != null &&
            CheckValidity(meta.EntityPrototype, component.LastDesiredPrototype))
        {
            nextItem = false;
            validItem = true;
            component.LastDesiredPrototype = null;
        }

        if (!validItem)
        {
            if (!HasComp<RefillableSolutionComponent>(args.Used) &&
                component.RejectAccumulator >= component.RejectTime.TotalSeconds)
            {
                component.RejectAccumulator = 0;
                _chat.TrySendInGameICMessage(uid, _random.Pick(component.RejectMessages), InGameICChatType.Speak, true);
            }
            return;
        }

        EntityManager.QueueDeleteEntity(args.Used);

        EntityManager.SpawnEntity("ResearchDisk5000", Transform(args.User).Coordinates);

        DispenseLiquidReward(uid, component);

        var i = _random.Next(1, 4);

        while (i != 0)
        {
            EntityManager.SpawnEntity("MaterialBluespace1", Transform(args.User).Coordinates);
            i--;
        }

        if (nextItem)
            NextItem(component);
    }

    private bool CheckValidity(EntityPrototype given, EntityPrototype target)
    {
        // 1: directly compare Names
        // name instead of ID because the oracle asks for them by name
        // this could potentially lead to like, labeller exploits maybe but so far only mob names can be fully player-set.
        if (given.Name == target.Name)
            return true;

        return false;
    }

    private void DispenseLiquidReward(EntityUid uid, OracleComponent component)
    {
        if (!_solutionSystem.TryGetSolution(uid, OracleComponent.SolutionName, out var fountainSol))
            return;

        var allReagents = _prototypeManager.EnumeratePrototypes<ReagentPrototype>()
            .Where(x => !x.Abstract)
            .Select(x => x.ID).ToList();

        var amount = 20 + _random.Next(1, 30) + _glimmerSystem.Glimmer / 10f;
        amount = (float) Math.Round(amount);

        var sol = new Solution();
        var reagent = "";

        if (_random.Prob(0.2f))
            reagent = _random.Pick(allReagents);
        else
            reagent = _random.Pick(component.RewardReagents);

        sol.AddReagent(reagent, amount);

        _solutionSystem.TryMixAndOverflow(fountainSol.Value, sol, fountainSol.Value.Comp.Solution.MaxVolume, out var overflowing);

        if (overflowing != null && overflowing.Volume > 0)
            _puddleSystem.TrySpillAt(uid, overflowing, out var _);
    }

    private void NextItem(OracleComponent component)
    {
        component.Accumulator = 0;
        component.BarkAccumulator = 0;
        component.RejectAccumulator = 0;
        var protoString = GetDesiredItem(component);
        if (_prototypeManager.TryIndex<EntityPrototype>(protoString, out var proto))
            component.DesiredPrototype = proto;
        else
            Logger.Error("Oracle can't index prototype " + protoString);
    }

    private string GetDesiredItem(OracleComponent component)
    {
        return _random.Pick(GetAllProtos(component));
    }


    public List<string> GetAllProtos(OracleComponent component)
    {
        var allTechs = _prototypeManager.EnumeratePrototypes<TechnologyPrototype>();
        var allRecipes = new List<string>();

        foreach (var tech in allTechs)
        {
            foreach (var recipe in tech.RecipeUnlocks)
            {
                var recipeProto = _prototypeManager.Index(recipe);
                allRecipes.Add(recipeProto.Result);
            }
        }

        var allPlants = _prototypeManager.EnumeratePrototypes<SeedPrototype>().Select(x => x.ProductPrototypes[0])
            .ToList();
        var allProtos = allRecipes.Concat(allPlants).ToList();
        var blacklist = component.BlacklistedPrototypes.ToList();

        foreach (var proto in allProtos)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(proto, out var entityProto))
            {
                blacklist.Add(proto);
                continue;
            }

            if (!entityProto.Components.ContainsKey("Item"))
            {
                blacklist.Add(proto);
                continue;
            }

            if (entityProto.Components.ContainsKey("SolutionTransfer"))
            {
                blacklist.Add(proto);
                continue;
            }

            if (entityProto.Components.ContainsKey("MobState"))
                blacklist.Add(proto);
        }

        foreach (var proto in blacklist)
        {
            allProtos.Remove(proto);
        }

        return allProtos;
    }
}
