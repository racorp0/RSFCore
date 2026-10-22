using RSF.Core.Common;
using RSF.Core.Components;

namespace RSF.Core.Components;

public sealed record LandLeaseComponent(
    EntityId TenantId,
    GameMinute StartMinute,
    GameMinute ExpiryMinute
) : IComponent;