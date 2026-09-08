using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaSmokeAbilitySystem : EntitySystem
{
    public virtual bool TrySpawnNinjaSmoke(Entity<NinjaSmokeAbilityComponent> ent, bool autoMode)
    {
        return false;
    }
}