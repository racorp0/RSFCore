using RSF.Core.Common;

namespace RSF.Core.Components;

public sealed record OwnershipComponent(
    EntityId OwnerId
) : IComponent;