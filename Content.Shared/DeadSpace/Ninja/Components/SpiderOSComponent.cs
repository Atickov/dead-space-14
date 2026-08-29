using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpiderOSComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<int> LockedTiers = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, string> SelectedModules = new();
}

[Serializable, NetSerializable]
public enum SpiderOSUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SpiderOSSelectModuleMessage : BoundUserInterfaceMessage
{
    public int Tier;
    public string ModuleId = string.Empty;

    public SpiderOSSelectModuleMessage()
    {
    }

    public SpiderOSSelectModuleMessage(int tier, string moduleId)
    {
        Tier = tier;
        ModuleId = moduleId;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderOSBoundUserInterfaceState : BoundUserInterfaceState
{
    public HashSet<int> LockedTiers = new();
    public Dictionary<int, string> SelectedModules = new();

    public SpiderOSBoundUserInterfaceState()
    {
    }

    public SpiderOSBoundUserInterfaceState(HashSet<int> lockedTiers, Dictionary<int, string> selectedModules)
    {
        LockedTiers = lockedTiers;
        SelectedModules = selectedModules;
    }
}