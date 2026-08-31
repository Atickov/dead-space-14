// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

<<<<<<<< HEAD:Content.Shared/DeadSpace/Hooligan/Roles/HooliganRoleComponent.cs
namespace Content.Shared.DeadSpace.Hooligan.Roles;

/// <summary>
/// Вешается на сущность разума игрока.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HooliganRoleComponent : BaseMindRoleComponent;
========
namespace Content.Shared.DeadSpace.PipeShuttle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PipeShuttleCallComponent : Component
{
    [DataField]
    public EntityUid? Shuttle;
}
>>>>>>>> master:Content.Shared/DeadSpace/PipeShuttle/Components/PipeShuttleCallComponent.cs
