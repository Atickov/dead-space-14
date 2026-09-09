using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Server.GameObjects;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class SpiderOSSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<SpiderOSComponent>(SpiderOSUiKey.Key, subs => { subs.Event<SpiderOSSelectModuleMessage>(OnSelectModule); });
        SubscribeLocalEvent<SpiderOSComponent, BoundUIOpenedEvent>(OnBuiOpened);
    }

    private void OnBuiOpened(Entity<SpiderOSComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is SpiderOSUiKey key && key.Equals(SpiderOSUiKey.Key))
        {
            UpdateUi(ent.Owner, ent.Comp);
        }
    }

    private void OnSelectModule(Entity<SpiderOSComponent> suit, ref SpiderOSSelectModuleMessage args)
    {
        var (suitUid, comp) = suit;


        if (comp.LockedTiers.Contains(args.Tier) || comp.SelectedModules.ContainsKey(args.Tier))
        {
            return;
        }

        comp.SelectedModules[args.Tier] = args.ModuleId;
        comp.LockedTiers.Add(args.Tier);

        GrantModule(suitUid, args.Tier, args.ModuleId);

        var wearer = _transform.GetParentUid(suitUid);
        if (wearer.IsValid())
        {
            _actions.GrantContainedActions(wearer, suitUid);
        }

        Dirty(suitUid, comp);
        UpdateUi(suitUid, comp);
    }

    private void GrantModule(EntityUid suitUid, int tier, string category)
    {
        switch (category)
        {
            case "Ghost":
                switch (tier)
                {
                    case 1:
                        EnsureComp<NinjaSmokeAbilityComponent>(suitUid);
                        break;
                    case 2:
                        EnsureComp<NinjaScannerComponent>(suitUid);
                        break;
                    case 5:
                        EnsureComp<NinjaSpiritFormComponent>(suitUid);
                        break;
                }
                break;

            case "Snake":
                switch (tier)
                {
                    case 1:
                        EnsureComp<NinjaJohyoAbilityComponent>(suitUid);
                        break;
                    case 2:
                        EnsureComp<NinjaHealingCocktailAbilityComponent>(suitUid);
                        break;
                    case 3:
                        EnsureComp<NinjaEmergencyTeleportComponent>(suitUid);
                        break;
                }
                break;

            case "Steel":
                switch (tier)
                {
                    case 1:
                        EnsureComp<NinjaShurikenAbilityComponent>(suitUid);
                        break;
                    case 2:
                        EnsureComp<NinjaAdrenalAbilityComponent>(suitUid);
                        break;
                    case 3:
                        EnsureComp<NinjaEmpAbilityComponent>(suitUid);
                        break;
                }
                break;
        }
    }

    private void UpdateUi(EntityUid suitUid, SpiderOSComponent comp)
    {
        var state = new SpiderOSBoundUserInterfaceState(comp.LockedTiers, comp.SelectedModules);
        _ui.SetUiState(suitUid, SpiderOSUiKey.Key, state);
    }
}