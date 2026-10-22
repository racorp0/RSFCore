using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Collects advance bed payments in exact daily periods and releases unpaid entitlements.</summary>
public sealed class InnSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    // Keep the original renewal order when several guests compete for the remaining beds.
    private List<Entity> _guests = new();
    private readonly HashSet<EntityId> _guestIds = new();
    /// <summary>Connects rental accounting to existing gold and the game clock.</summary>
    /// <param name="world">The inn world.</param>
    /// <param name="clock">The shared clock.</param>
    public InnSystem(World world, WorldClock clock) { _world = world; _clock = clock; }

    /// <summary>Checks indexed guest stays and reserves an available bed for 1440 minutes after payment.</summary>
    /// <param name="guest">The living guest paying in advance.</param>
    /// <param name="inn">The inn with a living owner and free capacity.</param>
    /// <returns>True if a paid entitlement exists after the request.</returns>
    public bool Rent(Entity guest, Entity inn)
    {
        if (!VillageRules.Near(_world, guest, inn.Id) || !_world.TryGet<HealthComponent>(guest, out var hp) || hp.Current <= 0 ||
            !_world.TryGet<VillageResidentComponent>(guest, out var resident) || resident is null ||
            !_world.TryGet<BuildingComponent>(inn, out var building) || building is null || building.Kind != BuildingKind.Inn) return false;
        if (_world.TryGet<InnStayComponent>(guest, out var stay) && stay.InnId == inn.Id && _clock.CurrentMinute.Value < stay.PaidUntil.Value) return true;
        int occupied = _world.GetEntitiesWith<InnStayComponent>().Count(e => _world.TryGet<InnStayComponent>(e, out var s) &&
            s.InnId == inn.Id && s.PaidUntil.Value > _clock.CurrentMinute.Value);
        if (occupied >= building.Capacity || !Pay(guest, inn, resident.Village.RentPerDay)) return false;
        _world.Add(guest, new InnStayComponent(inn.Id, _clock.CurrentMinute.Add(GameDate.MinutesPerDay)));
        return true;
    }

    /// <summary>Renews indexed stays in world order once per paid day; failed renewal wakes the guest without debt.</summary>
    /// <param name="deltaTime">The simulation step.</param>
    public void Update(float deltaTime)
    {
        // A stable list allows Release to remove stays without changing the collection being enumerated.
        foreach (Entity guest in GetGuests())
        {
            if (!_world.TryGet<InnStayComponent>(guest, out var stay)) continue;
            if (_world.Has<DeadComponent>(guest) || !_world.TryGet<HealthComponent>(guest, out var hp) || hp.Current <= 0 ||
                !_world.TryGetEntity(stay.InnId, out var inn) || inn is null ||
                !_world.TryGet<VillageResidentComponent>(guest, out var resident) || resident is null ||
                (_world.TryGet<MerchantVisitComponent>(guest, out var visit) && visit is not null && visit.Departing))
            { Release(guest); continue; }
            while (_clock.CurrentMinute.Value >= stay.PaidUntil.Value)
            {
                int occupied = _world.GetEntitiesWith<InnStayComponent>().Count(e => e != guest && _world.TryGet<InnStayComponent>(e, out var other) &&
                    other.InnId == inn.Id && other.PaidUntil.Value > _clock.CurrentMinute.Value);
                if (!_world.TryGet<BuildingComponent>(inn, out var beds) || beds is null || occupied >= beds.Capacity ||
                    !Pay(guest, inn, resident.Village.RentPerDay)) { Release(guest); break; }
                stay = stay with { PaidUntil = stay.PaidUntil.Add(GameDate.MinutesPerDay) };
                _world.Add(guest, stay);
            }
        }
    }

    /// <summary>Finds current guests through the component index and preserves their original world order.</summary>
    /// <returns>A stable guest list, rebuilt only when stay membership changes.</returns>
    private List<Entity> GetGuests()
    {
        List<Entity> current = _world.GetEntitiesWith<InnStayComponent>().ToList();
        if (current.Count != _guestIds.Count || current.Any(guest => !_guestIds.Contains(guest.Id)))
        {
            _guestIds.Clear();
            foreach (Entity guest in current) _guestIds.Add(guest.Id);
            // Component attachment order can differ from world order and must not change who renews first.
            _guests = current.Count == 0 ? current
                : _world.GetEntities().Where(entity => _guestIds.Contains(entity.Id)).ToList();
        }
        return _guests;
    }

    /// <summary>Releases an entitlement without refunding the paid portion of the day.</summary>
    /// <param name="guest">The guest leaving or losing access.</param>
    public void Release(Entity guest)
    {
        _world.Remove<InnStayComponent>(guest);
        _world.Remove<SleepingComponent>(guest);
        _world.Add(guest, new FatigueClockComponent(_clock.CurrentMinute));
    }

    /// <summary>Validates and transfers rent with overflow protection, then records both balances for diagnosis.</summary>
    /// <param name="guest">The payer.</param>
    /// <param name="inn">The owned inn.</param>
    /// <param name="price">The positive daily rent.</param>
    /// <returns>True only after both balances have been updated.</returns>
    private bool Pay(Entity guest, Entity inn, int price)
    {
        if (price <= 0 || !_world.TryGet<OwnershipComponent>(inn, out var ownership) || ownership is null ||
            !_world.TryGetEntity(ownership.OwnerId, out var owner) || owner is null || owner == guest ||
            !_world.TryGet<HealthComponent>(owner, out var hp) || hp.Current <= 0 ||
            !_world.TryGet<GoldComponent>(guest, out var money) || money.Amount < price ||
            !_world.TryGet<GoldComponent>(owner, out var income) || income.Amount < 0 || (long)income.Amount + price > int.MaxValue) return false;
        _world.Add(guest, new GoldComponent(money.Amount - price));
        _world.Add(owner, new GoldComponent(income.Amount + price));
        TransactionHistory.Rent(_world, guest, owner, inn, _clock.CurrentMinute, price, money.Amount, income.Amount);
        return true;
    }
}
