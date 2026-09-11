// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ThermalVision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server.DeadSpace.ThermalVision;

public sealed class ThermalVisorExperimentalSystem : EntitySystem
{
    public const SlotFlags ValidSlots = SlotFlags.EYES;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThermalVisorExperimentalComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<ThermalVisorExperimentalComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(EntityUid entity, ThermalVisorExperimentalComponent comp, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ValidSlots) == 0)
            return;

        if (HasComp<ThermalVisionExperimentalComponent>(args.Equipee))
            return;

        var activeComp = new ThermalVisionExperimentalComponent
        {
            ActivateSound = comp.ActivateSound,
            ActivateSoundOff = comp.ActivateSoundOff,
            VisorUid = entity,
            PulseDuration = comp.PulseDuration,
            UseShader = comp.UseShader
        };
        comp.HasThermalVision = true;

        AddComp(args.Equipee, activeComp);
    }

    private void OnGotUnequipped(EntityUid entity, ThermalVisorExperimentalComponent comp, ref GotUnequippedEvent args)
    {
        if (comp.HasThermalVision && HasComp<ThermalVisionExperimentalComponent>(args.Equipee))
        {
            if (TryComp<ThermalVisionExperimentalComponent>(args.Equipee, out var activeComp))
                activeComp.IsActive = false;

            RemComp<ThermalVisionExperimentalComponent>(args.Equipee);
        }
    }

    private void OnRemove(EntityUid uid, ThermalVisionExperimentalComponent component, ComponentRemove args)
    {
        _actions.RemoveAction(uid, component.ActionToggleThermalVisionExperimentalEntity);
    }

    private void OnExamine(EntityUid uid, ThermalVisorExperimentalComponent component, ExaminedEvent args)
    {
        if (!TryComp<BatteryComponent>(uid, out var battery))
            return;

        var charge = _battery.GetCharge((uid, battery));
        var uses = (int)(charge / EnergyPerUse);
        string color;

        if (uses == 0)
            color = "red";
        else if (uses <= 5)
            color = "yellow";
        else
            color = "green";

        args.PushMarkup(Loc.GetString("thermal-visor-experimental-examine-charges",
            ("color", color),
            ("currentUses", uses)));
    }

    private void OnToggle(EntityUid uid, ThermalVisionExperimentalComponent component, ToggleThermalVisionExperimentalActionEvent args)
    {
        if (args.Handled || component.IsActive)
            return;

        var visorUid = component.VisorUid;
        if (visorUid == null || !TryComp<BatteryComponent>(visorUid.Value, out var battery))
            return;

        if (_battery.GetCharge((visorUid.Value, battery)) < EnergyPerUse)
            return;

        args.Handled = true;
        _battery.UseCharge((visorUid.Value, battery), EnergyPerUse);
        component.IsActive = true;
        component.CurrentPulseTime = component.PulseDuration;
        Dirty(uid, component);

        if (TryComp<ThermalVisorExperimentalComponent>(visorUid.Value, out var visorComp))
        {
            visorComp.LastToggleTime = _timing.CurTime;
            Dirty(visorUid.Value, visorComp);
        }

        Timer.Spawn(TimeSpan.FromSeconds(component.PulseDuration), () =>
        {
            if (!Exists(uid))
                return;

            if (TryComp<ThermalVisionExperimentalComponent>(uid, out var comp))
            {
                comp.IsActive = false;
                Dirty(uid, comp);
            }
        });
    }
}