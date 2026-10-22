
using RSF.Core.Components;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Removes expired land lease components during RSF simulation updates without changing ownership.</summary>
public sealed class LeaseSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;

    /// <summary>Connects lease expiry checks to the world and shared game clock.</summary>
    /// <param name="world">The world containing leased land.</param>
    /// <param name="clock">The clock used to compare lease expiry minutes.</param>
    public LeaseSystem(
        World world,
        WorldClock clock)
    {
        _world = world;
        _clock = clock;
    }

    /// <summary>Checks indexed land leases and removes those whose expiry minute has been reached.</summary>
    /// <param name="deltaTime">The simulation step; expiry uses the shared clock.</param>
    public void Update(float deltaTime)
    {
        // Skip entities without leases. Copy first because expiry removes entries from the lease index.
        foreach (var entity in _world.GetEntitiesWith<LandLeaseComponent>().ToList())
        {
            if (!_world.TryGet<LandLeaseComponent>(
                    entity,
                    out var lease))
            {
                continue;
            }

            if (_clock.CurrentMinute.Value <
                lease.ExpiryMinute.Value)
            {
                continue;
            }

            _world.Remove<LandLeaseComponent>(entity);

            Console.WriteLine(
                $"Lease expired for land {entity.Id}.");
        }
    }
}
