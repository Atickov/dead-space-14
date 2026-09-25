// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Clothing;
using Content.Client.Inventory;
using Content.Client.Strip;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaDisguiseClientSystem : SharedNinjaDisguiseSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly ClientClothingSystem _clothing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly Dictionary<EntityUid, HashSet<string>> _activeDisguiseKeys = new();
    private readonly HashSet<EntityUid> _disguisedWearers = new();
    private readonly HashSet<EntityUid> _activeProxies = new();
    private readonly HashSet<string> _disguiseHiddenSlots = new();
    private readonly Dictionary<(EntityUid Wearer, string Slot), (EntityUid Proxy, Vector2 SlotOffset)> _disguiseProxies = new();

    private (EntityUid Suit, EntityUid Wearer)? _disguiseContext;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_disguiseContext is not { } ctx || !Exists(ctx.Suit))
            return;

        if (ActiveDisguiseKeysIntact(ctx.Wearer))
        {
            EnforceDisguiseLayerVisibility(ctx.Wearer);
            return;
        }

        ReapplyDisguise(ctx);
    }

    private bool ActiveDisguiseKeysIntact(EntityUid wearer)
    {
        if (!_activeDisguiseKeys.TryGetValue(wearer, out var keys) ||
            keys.Count == 0 ||
            !TryComp<SpriteComponent>(wearer, out var sprite))
        {
            return true;
        }

        foreach (var key in keys)
        {
            if (!_sprite.LayerMapTryGet((wearer, sprite), key, out _, false))
                return false;
        }

        return true;
    }

    private void ReapplyDisguise((EntityUid Suit, EntityUid Wearer) ctx)
    {
        if (!TryComp<NinjaDisguiseComponent>(ctx.Suit, out var comp))
            return;

        Log.Debug($"Ninja disguise: re-applying clothing on entity {ctx.Wearer}.");
        RefreshWearer((ctx.Suit, comp));
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaDisguiseComponent, AfterAutoHandleStateEvent>(OnSuitState);
        SubscribeLocalEvent<NinjaDisguiseComponent, ComponentShutdown>(OnSuitShutdown);
        SubscribeLocalEvent<ItemComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: new[] { typeof(ClientClothingSystem) });
    }

    private void OnSuitState(Entity<NinjaDisguiseComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshWearer(ent);
    }

    private void OnSuitShutdown(Entity<NinjaDisguiseComponent> ent, ref ComponentShutdown args)
    {
        RefreshWearer(ent);
    }

    private void RefreshWearer(Entity<NinjaDisguiseComponent> ent)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid() || !Exists(wearer))
        {
            _disguiseContext = null;
            return;
        }

        var comp = ent.Comp;
        if (comp.Disguised && comp.ActiveIndex is { } index &&
            index >= 0 && index < comp.Entries.Count)
        {
            _disguiseContext = (ent.Owner, wearer);
            _disguisedWearers.Add(wearer);
            ApplyClothing(wearer, comp.Entries[index]);
            DirtyStripUi(wearer);
            return;
        }

        _disguiseContext = null;
        RestoreClothing(wearer);
        _disguisedWearers.Remove(wearer);
        DirtyStripUi(wearer);
    }

    private void DirtyStripUi(EntityUid wearer)
    {
        EntityManager.System<StrippableSystem>().UpdateUi(wearer);
    }

    public bool IsDisguised(EntityUid wearer)
    {
        return _disguisedWearers.Contains(wearer);
    }

    public bool TryGetDisguiseSlotProxy(EntityUid wearer, string slot, out EntityUid proxy)
    {
        proxy = default;
        if (!_disguiseProxies.TryGetValue((wearer, slot), out var entry) || !Exists(entry.Proxy))
            return false;

        proxy = entry.Proxy;
        return true;
    }

    public bool TryGetDisguiseSlotProto(EntityUid wearer, string slot, out EntProtoId protoId)
    {
        protoId = default;
        if (!TryGetActiveDisguise(wearer, out _, out var comp) ||
            comp.ActiveIndex is not { } index ||
            index < 0 || index >= comp.Entries.Count)
        {
            return false;
        }

        foreach (var item in comp.Entries[index].Inventory)
        {
            if (item.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase) && item.ItemId is { } itemId)
            {
                protoId = itemId;
                return true;
            }
        }

        return false;
    }

    private void ApplyClothing(EntityUid wearer, NinjaDisguiseEntry entry)
    {
        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        ClearEquipmentLayers(wearer, sprite);
        _activeDisguiseKeys.Remove(wearer);
        _disguiseHiddenSlots.Clear();
        DeleteActiveProxies();

        foreach (var hiddenSlot in entry.HiddenClothingSlots)
            _disguiseHiddenSlots.Add(hiddenSlot);

        foreach (var item in entry.Inventory)
        {
            if (!_proto.HasIndex<EntityPrototype>(item.ItemId))
                continue;

            if (ContainsSlot(_disguiseHiddenSlots, item.Slot))
                continue;

            var proxy = Spawn(item.ItemId.Id, MapCoordinates.Nullspace);
            if (!HasComp<ClothingComponent>(proxy))
            {
                QueueDel(proxy);
                continue;
            }

            _activeProxies.Add(proxy);
            _disguiseProxies[(wearer, item.Slot)] = (proxy, item.SlotOffset);

            _clothing.RenderEquipment(wearer, proxy, item.Slot, slotOffset: item.SlotOffset);

            if (TryComp<InventorySlotsComponent>(wearer, out var slots) &&
                slots.VisualLayerKeys.TryGetValue(item.Slot, out var revealed))
            {
                foreach (var key in revealed)
                    ActiveKeys(wearer).Add(key);
            }
        }

        EnforceDisguiseLayerVisibility(wearer);
    }

    private void EnforceDisguiseLayerVisibility(EntityUid wearer)
    {
        if (!TryComp<InventorySlotsComponent>(wearer, out var slots) ||
            !TryComp<SpriteComponent>(wearer, out var sprite) ||
            !_activeDisguiseKeys.TryGetValue(wearer, out var activeKeys))
        {
            return;
        }

        foreach (var (slot, layerKeys) in slots.VisualLayerKeys)
        {
            var visible = !ContainsSlot(_disguiseHiddenSlots, slot);
            foreach (var key in layerKeys)
            {
                if (!activeKeys.Contains(key))
                    continue;

                if (_sprite.LayerMapTryGet((wearer, sprite), key, out var layer, false))
                    _sprite.LayerSetVisible((wearer, sprite), layer, visible);
            }
        }
    }

    private static bool ContainsSlot(IEnumerable<string> slots, string slot)
    {
        foreach (var candidate in slots)
        {
            if (candidate.Equals(slot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void RestoreClothing(EntityUid wearer)
    {
        if (TryComp<SpriteComponent>(wearer, out var sprite))
        {
            RemoveActiveDisguiseLayers(wearer, sprite);
        }

        _disguiseHiddenSlots.Clear();
        DeleteActiveProxies();

        if (TryComp<InventoryComponent>(wearer, out var inventory))
            _clothing.InitClothing(wearer, inventory);
    }

    private void RemoveActiveDisguiseLayers(EntityUid wearer, SpriteComponent sprite)
    {
        if (!_activeDisguiseKeys.Remove(wearer, out var keys))
            return;

        foreach (var key in keys)
            _sprite.RemoveLayer((wearer, sprite), key, logMissing: false);

        if (TryComp(wearer, out InventorySlotsComponent? slots))
        {
            foreach (var revealed in slots.VisualLayerKeys.Values)
                revealed.RemoveWhere(key => keys.Contains(key));
        }
    }

    private void DeleteActiveProxies()
    {
        foreach (var proxy in _activeProxies)
        {
            if (Exists(proxy))
                Del(proxy);
        }

        _activeProxies.Clear();
        _disguiseProxies.Clear();
    }

    private HashSet<string> ActiveKeys(EntityUid wearer)
    {
        if (!_activeDisguiseKeys.TryGetValue(wearer, out var keys))
            _activeDisguiseKeys[wearer] = keys = new HashSet<string>();

        return keys;
    }

    private void ClearEquipmentLayers(EntityUid wearer, SpriteComponent sprite)
    {
        if (!TryComp<InventorySlotsComponent>(wearer, out var slots))
            return;

        foreach (var revealed in slots.VisualLayerKeys.Values)
        {
            foreach (var key in revealed)
                _sprite.RemoveLayer((wearer, sprite), key, logMissing: false);

            revealed.Clear();
        }
    }

    private void OnGetEquipmentVisuals(Entity<ItemComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (_activeProxies.Contains(ent.Owner))
            return;

        if (_disguiseContext is not { } ctx || ctx.Wearer != args.Equipee)
            return;

        args.Layers.Clear();

        RestoreSlotDisguise(args.Equipee, args.Slot);
    }

    private void RestoreSlotDisguise(EntityUid wearer, string slot)
    {
        if (!_disguiseProxies.TryGetValue((wearer, slot), out var entry) ||
            !Exists(entry.Proxy) ||
            !TryComp<SpriteComponent>(wearer, out _))
        {
            return;
        }

        _clothing.RenderEquipment(wearer, entry.Proxy, slot, slotOffset: entry.SlotOffset);

        if (!TryComp<InventorySlotsComponent>(wearer, out var slots) ||
            !slots.VisualLayerKeys.TryGetValue(slot, out var revealed))
        {
            return;
        }

        foreach (var key in revealed)
            ActiveKeys(wearer).Add(key);

        if (ContainsSlot(_disguiseHiddenSlots, slot) &&
            TryComp<SpriteComponent>(wearer, out var sprite))
        {
            foreach (var key in revealed)
            {
                if (_sprite.LayerMapTryGet((wearer, sprite), key, out var layer, false))
                    _sprite.LayerSetVisible((wearer, sprite), layer, false);
            }
        }
    }
}