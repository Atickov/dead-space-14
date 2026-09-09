using System.Diagnostics.CodeAnalysis;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Prototypes;
using Content.Shared.DeadSpace.Ninja.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class SpiderOSSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedNinjaAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SpiderOSComponent>(SpiderOSUiKey.Key, subs =>
        {
            subs.Event<SpiderOSSelectModuleMessage>(OnSelectModule);
            subs.Event<SpiderOSSetAppearanceMessage>(OnSetAppearance);
            subs.Event<SpiderOSSetSuitPowerMessage>(OnSetSuitPower);
        });

        SubscribeLocalEvent<SpiderOSComponent, BoundUIOpenedEvent>(OnBuiOpened);
        SubscribeLocalEvent<SpiderOSComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SpiderOSComponent> ent, ref MapInitEvent args)
    {
        foreach (var action in ent.Comp.Actions)
        {
            AddActionToContainer(ent.Owner, action);
        }
    }

    private void OnBuiOpened(Entity<SpiderOSComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not SpiderOSUiKey.Key)
        {
            return;
        }

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnSelectModule(Entity<SpiderOSComponent> suit, ref SpiderOSSelectModuleMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor) ||
            comp.SuitActivated ||
            !TryGetSkill(comp, args.Category, args.Tier, out _))
        {
            return;
        }

        if (comp.LockedTiers.Contains(args.Tier) || comp.SelectedModules.ContainsKey(args.Tier))
        {
            return;
        }

        comp.SelectedModules[args.Tier] = args.Category;
        comp.LockedTiers.Add(args.Tier);

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void OnSetAppearance(Entity<SpiderOSComponent> suit, ref SpiderOSSetAppearanceMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor) ||
            comp.SuitActivated ||
            args.Colorway is < NinjaColorway.Red or > NinjaColorway.Green)
        {
            return;
        }

        if (comp.PendingColorway == args.Colorway && comp.PendingHelmet == args.Helmet)
        {
            return;
        }

        comp.PendingColorway = args.Colorway;
        comp.PendingHelmet = args.Helmet;

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void OnSetSuitPower(Entity<SpiderOSComponent> suit, ref SpiderOSSetSuitPowerMessage args)
    {
        var (suitUid, comp) = suit;

        if (!IsAuthorized(suitUid, args.Actor))
        {
            return;
        }

        if (args.Activated)
        {
            if (comp.SuitActivated)
            {
                return;
            }

            comp.SuitActivated = true;
            ApplyPendingAppearance(suitUid, comp);

            foreach (var (tier, category) in comp.SelectedModules)
            {
                if (comp.ActivatedTiers.Contains(tier) || !TryGetSkill(comp, category, tier, out var skill))
                {
                    continue;
                }

                GrantSkillComponents(suitUid, skill);
                GrantSkillActions(suitUid, skill);
                comp.ActivatedTiers.Add(tier);
            }

            var wearer = _transform.GetParentUid(suitUid);
            if (wearer.IsValid())
            {
                _actions.GrantContainedActions(wearer, suitUid);
            }
        }
        else
        {
            if (!comp.SuitActivated)
            {
                return;
            }

            comp.SuitActivated = false;

            var wearer = _transform.GetParentUid(suitUid);
            if (wearer.IsValid())
            {
                RemoveGrantedActions(suitUid, wearer, comp);
            }
        }

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void ApplyPendingAppearance(EntityUid suitUid, SpiderOSComponent comp)
    {
        if (TryComp<NinjaAppearanceComponent>(suitUid, out var appearance))
        {
            _appearance.SetAppearance((suitUid, appearance), comp.PendingColorway, comp.PendingHelmet);
        }
    }

    private bool IsAuthorized(EntityUid suitUid, EntityUid actor)
    {
        if (!actor.IsValid() || actor != _transform.GetParentUid(suitUid))
            return false;

        if (!_ninja.NinjaQuery.TryComp(actor, out var ninja))
            return false;

        return ninja.Suit == suitUid;
    }

    private bool TryGetSkill(SpiderOSComponent comp, NinjaSkillsCategory category, int tier, [NotNullWhen(true)] out NinjaSkill skill)
    {
        skill = default;

        if (tier < 1 || !Enum.IsDefined(typeof(NinjaSkillsCategory), category))
        {
            return false;
        }

        if (!_proto.TryIndex(comp.Skills.Id, out SpiderOSPrototype? proto))
        {
            return false;
        }

        foreach (var candidate in proto.AllSkills)
        {
            if (candidate.Category == category && candidate.Tier == tier)
            {
                skill = candidate;
                return true;
            }
        }

        return false;
    }

    private void GrantSkillComponents(EntityUid suitUid, NinjaSkill skill)
    {
        var toAdd = new ComponentRegistry();
        foreach (var (name, entry) in skill.Components)
        {
            if (!EntityManager.HasComponent(suitUid, entry.Component.GetType()))
            {
                toAdd[name] = entry;
            }
        }

        if (toAdd.Count > 0)
        {
            EntityManager.AddComponents(suitUid, toAdd);
        }
    }

    private void GrantSkillActions(EntityUid suitUid, NinjaSkill skill)
    {
        if (skill.Actions == null || skill.Actions.Count == 0)
        {
            return;
        }

        foreach (var actionProto in skill.Actions)
        {
            AddActionToContainer(suitUid, actionProto);
        }
    }

    private void AddActionToContainer(EntityUid suitUid, EntProtoId actionProto)
    {
        var container = EnsureComp<ActionsContainerComponent>(suitUid);

        foreach (var existing in container.Container.ContainedEntities)
        {
            if (MetaData(existing).EntityPrototype?.ID == actionProto.Id)
            {
                return;
            }
        }

        _actionContainer.AddAction(suitUid, actionProto, container);
    }

    private void RemoveGrantedActions(EntityUid suitUid, EntityUid wearer, SpiderOSComponent comp)
    {
        var grantedProtos = new HashSet<string>();
        foreach (var action in comp.Actions)
        {
            grantedProtos.Add(action.Id);
        }

        foreach (var (tier, category) in comp.SelectedModules)
        {
            if (!TryGetSkill(comp, category, tier, out var skill) || skill.Actions == null)
            {
                continue;
            }

            foreach (var action in skill.Actions)
            {
                grantedProtos.Add(action.Id);
            }
        }

        if (!TryComp<ActionsContainerComponent>(suitUid, out var container))
        {
            return;
        }

        foreach (var contained in container.Container.ContainedEntities)
        {
            if (MetaData(contained).EntityPrototype?.ID is { } protoId && grantedProtos.Contains(protoId))
            {
                _actions.RemoveProvidedAction(wearer, suitUid, contained);
            }
        }
    }

    private void UpdateUi(EntityUid suitUid, SpiderOSComponent comp)
    {
        var state = new SpiderOSBoundUserInterfaceState(
            comp.LockedTiers,
            comp.SelectedModules,
            comp.ActivatedTiers,
            comp.Skills.Id,
            comp.PendingColorway,
            comp.PendingHelmet,
            comp.SuitActivated);
        _ui.SetUiState(suitUid, SpiderOSUiKey.Key, state);
    }
}