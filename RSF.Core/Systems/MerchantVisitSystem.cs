using RSF.Core.Configuration;
using RSF.Core.Blueprints;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Schedules infrequent visitors and requests departure when trading is exhausted or a visit is too long.</summary>
public sealed class MerchantVisitSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly Random _random;
    private VillageComponent? _village;
    // Preserve visitor evaluation order when departure removes goods from sale.
    private List<Entity> _visitors = new();
    private readonly HashSet<EntityId> _visitorIds = new();
    /// <summary>Connects visitor timing to game minutes without real-time waits.</summary>
    /// <param name="world">The village world.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="random">Optional repeatable random source for arrival tests.</param>
    public MerchantVisitSystem(World world, WorldClock clock, Random? random = null)
    { _world = world; _clock = clock; _random = random ?? new Random(); }

    /// <summary>Checks visits, gives departing travelers bounded journey-food shopping, and preserves finite arrivals.</summary>
    /// <param name="deltaTime">The simulation step.</param>
    public void Update(float deltaTime)
    {
        if (_village is null)
        {
            Entity? root = _world.GetEntities().FirstOrDefault(e => _world.Has<VillageComponent>(e));
            if (root is null || !_world.TryGet<VillageComponent>(root, out _village) || _village is null) return;
        }
        _village.CurrentMinute = _clock.CurrentMinute;
        if (_village.MaxMerchants == 0) return;
        List<Entity> visitors = GetVisitors();
        if (_village.NextMerchantAt is null) PlanNext();
        if (visitors.Count(e => _world.TryGet<HealthComponent>(e, out var hp) && hp.Current > 0) < _village.MaxMerchants && _clock.CurrentMinute.Value >= _village.NextMerchantAt!.Value.Value)
        {
            Spawn();
            PlanNext();
        }
        foreach (Entity visitor in visitors)
        {
            if (!_world.TryGet<MerchantVisitComponent>(visitor, out var visit) || visit is null) continue;
            if (!_world.TryGet<HealthComponent>(visitor, out var hp) || hp.Current <= 0) continue;
            bool tooLong = _clock.CurrentMinute.ElapsedSince(visit.ArrivedAt).Minutes >= _village.MaxVisitMinutes;
            bool idle = _clock.CurrentMinute.ElapsedSince(visit.LastTradeAt).Minutes >= _village.MerchantIdleMinutes;
            if (!visit.Departing && !visit.PreparingDeparture && (tooLong || (idle && !HasUsefulTrade(visitor, visit))))
            {
                visit.PreparingDeparture = true;
                visit.DepartureShoppingStartedAt = _clock.CurrentMinute;
                // Reuse AI review and command travel instead of teleporting the shopper to a seller.
                if (_world.TryGet<AIStateComponent>(visitor, out var shoppingAI) && shoppingAI is not null)
                { shoppingAI.NextDecisionAt = _clock.CurrentMinute; shoppingAI.NextActionAt = _clock.CurrentMinute; }
            }
            var food = GameSettings.Find(_world)?.Food ?? new FoodSettings { MerchantJourneyFoodTarget = 0 };
            bool supplied = visit.PreparingDeparture && FoodProductionRules.JourneyCount(_world, visitor) >= food.MerchantJourneyFoodTarget;
            bool shoppingEnded = visit.DepartureShoppingStartedAt is GameMinute started &&
                _clock.CurrentMinute.ElapsedSince(started).Minutes >= food.DepartureShoppingMinutes;
            if (!visit.Departing && visit.PreparingDeparture && (supplied || shoppingEnded))
            {
                visit.Departing = true;
                _world.Remove<InnStayComponent>(visitor);
                foreach (Entity item in Bag(visitor)) { _world.Remove<TradePriceComponent>(item); _world.Remove<SaleLocationComponent>(item); }
                if (_world.TryGet<AIStateComponent>(visitor, out var ai) && ai is not null)
                { ai.NextDecisionAt = _clock.CurrentMinute; ai.NextActionAt = _clock.CurrentMinute; }
            }
        }
    }

    /// <summary>Finds visitors through the component index, rebuilding their world order only when membership changes.</summary>
    /// <returns>A stable visitor list captured before any new merchant is spawned.</returns>
    private List<Entity> GetVisitors()
    {
        List<Entity> current = _world.GetEntitiesWith<MerchantVisitComponent>().ToList();
        if (current.Count != _visitorIds.Count || current.Any(visitor => !_visitorIds.Contains(visitor.Id)))
        {
            _visitorIds.Clear();
            foreach (Entity visitor in current) _visitorIds.Add(visitor.Id);
            // Departure changes sale markers, so retain the original order even if visit components were attached differently.
            _visitors = current.Count == 0 ? current
                : _world.GetEntities().Where(entity => _visitorIds.Contains(entity.Id)).ToList();
        }
        return _visitors;
    }

    /// <summary>Removes a departing merchant only after reaching the entrance, preserving a cargo summary.</summary>
    /// <param name="merchant">The departing visitor.</param>
    /// <returns>True after the visitor and owned cargo leave the active world.</returns>
    public bool Leave(Entity merchant)
    {
        if (_village is null || !_world.TryGet<MerchantVisitComponent>(merchant, out var visit) || visit is null || !visit.Departing ||
            !VillageRules.Near(_world, merchant, _village.EntranceId)) return false;
        _world.TryGet<NameComponent>(merchant, out var name);
        _world.TryGet<GoldComponent>(merchant, out var gold);
        string cargo = string.Join(", ", Bag(merchant).Select(item => _world.TryGet<ItemComponent>(item, out var data)
            ? data.Name + " x" + InventoryStacks.Count(_world, item) +
                (_world.TryGet<FishComponent>(item, out var fish) ? $" ({fish.WeightGrams} g)" : string.Empty) +
                (_world.TryGet<MealComponent>(item, out var meal) ? $" ({meal.Quality})" : string.Empty) : item.Id.ToString()));
        _village.DepartedMerchants.Add($"{name.Value}: {gold.Amount} gold; {cargo}; departed minute {_clock.CurrentMinute.Value}");
        foreach (Entity item in Bag(merchant).ToList()) _world.DestroyEntity(item.Id);
        Console.WriteLine($"{name.Value} left the village.");
        _world.DestroyEntity(merchant.Id);
        return true;
    }

    /// <summary>Chooses a new infrequent arrival delay, including fractional game days.</summary>
    private void PlanNext()
    {
        int delay = _random.Next(_village!.MinMerchantDays * GameDate.MinutesPerDay,
            _village.MaxMerchantDays * GameDate.MinutesPerDay + 1);
        _village.NextMerchantAt = _clock.CurrentMinute.Add(delay);
    }

    /// <summary>Spawns finite seeds and each JSON bait stock at the village entrance; manual worlds retain their worm stock.</summary>
    private void Spawn()
    {
        if (!_world.TryGetEntity(_village!.EntranceId, out var entrance) || entrance is null ||
            !_world.TryGet<PositionComponent>(entrance, out var position)) return;
        Entity visitor = new NpcBlueprint().Spawn(_world, "Traveling Merchant " + (++_village.MerchantsArrived),
            position.X, position.Y, Job.Merchant, work: new AIWorkSettingsComponent { BeforeWorkHungerPercent = 0 });
        _world.Add(visitor, new MerchantVisitComponent { ArrivedAt = _clock.CurrentMinute, LastTradeAt = _clock.CurrentMinute,
            FishTarget = _village.MerchantFishTarget });
        _world.Add(visitor, new GoldComponent(_village.MerchantGold));
        InventorySystem inventory = new(_world);
        foreach (var plant in GameSettings.Find(_world)?.Farming.Plants ?? Array.Empty<PlantDefinition>())
            if (plant.MerchantSeeds > 0) inventory.AddItem(visitor, new PlantSeedBlueprint().Spawn(_world, plant, plant.MerchantSeeds));
        var fishing = GameSettings.Find(_world)?.Fishing;
        if (fishing is not null)
        {
            foreach (var bait in fishing.Baits)
                if (bait.MerchantStock > 0) inventory.AddItem(visitor, new BaitBlueprint().Spawn(_world, bait.MerchantStock, bait.Id));
        }
        else if (_village.MerchantBait > 0) inventory.AddItem(visitor, new BaitBlueprint().Spawn(_world, _village.MerchantBait));
        if (_village.MerchantSpice > 0)
        {
            Entity spice = _world.CreateEntity(); _world.Add(spice, new ItemComponent("Spices"));
            _world.Add(spice, new SpiceComponent(_village.MerchantSpice)); inventory.AddItem(visitor, spice);
        }
        for (int i = 0; i < _village.MerchantBread; i++) inventory.AddItem(visitor, new BreadBlueprint().Spawn(_world));
        Console.WriteLine($"Traveling merchant {_village.MerchantsArrived} arrived at minute {_clock.CurrentMinute.Value}.");
    }

    /// <summary>Checks affordable commodity trades after reserving journey gold, leaving fresh survival food to normal needs.</summary>
    /// <param name="merchant">The visitor checking opportunities.</param>
    /// <param name="visit">The visitor's purchasing targets.</param>
    /// <returns>True when an affordable trade with a living customer is possible.</returns>
    private bool HasUsefulTrade(Entity merchant, MerchantVisitComponent visit)
    {
        _world.TryGet<GoldComponent>(merchant, out var wallet);
        int shoppingGold = Math.Max(0, wallet.Amount - FoodProductionRules.JourneyGoldReserve(_world, merchant));
        int fish = Bag(merchant).Count(e => !FoodRules.IsExpired(_world, e) && _world.Has<FishComponent>(e));
        // Every eligible partner already requires health. This read-only check can skip unrelated terrain through its index.
        foreach (Entity other in _world.GetEntitiesWith<HealthComponent>())
        {
            if (other == merchant || !_world.TryGet<HealthComponent>(other, out var hp) || hp.Current <= 0 ||
                !_world.TryGet<GoldComponent>(other, out var money)) continue;
            foreach (Entity item in Bag(other))
                if (!FoodRules.IsExpired(_world, item) && _world.Has<SaleLocationComponent>(item) &&
                    _world.TryGet<TradePriceComponent>(item, out var price) && price.GoldPerUnit > 0 && price.GoldPerUnit <= shoppingGold &&
                    (_world.Has<FishComponent>(item) && fish < visit.FishTarget)) return true;
            foreach (var definition in GameSettings.Find(_world)?.Farming.Plants ?? Array.Empty<PlantDefinition>())
            {
                if (_world.TryGet<JobComponent>(other, out var farmerJob) && farmerJob.Job == Job.Farmer && money.Amount >= definition.SeedPrice &&
                    Bag(other).Where(e => _world.TryGet<PlantSeedComponent>(e, out var seeds) && seeds.PlantId == definition.Id && !_world.Has<SaleLocationComponent>(e)).Sum(e => InventoryStacks.Count(_world, e)) < definition.SeedStockTarget &&
                    Bag(merchant).Any(e => _world.TryGet<PlantSeedComponent>(e, out var seeds) && seeds.PlantId == definition.Id && seeds.Quantity > 0)) return true;
            }
            foreach (var output in GameSettings.Find(_world)?.Products.Values ?? Enumerable.Empty<HarvestOutputDefinition>())
            {
                long held = InventoryStacks.CountItem(_world, Bag(merchant), output.ItemId);
                if (held < output.MerchantStockTarget && Bag(other).Any(e => _world.TryGet<PlantProduceComponent>(e, out var product) && product.ItemId == output.ItemId &&
                    !FoodRules.IsExpired(_world, e) && _world.Has<SaleLocationComponent>(e) &&
                    _world.TryGet<TradePriceComponent>(e, out var cost) && cost.GoldPerUnit > 0 && shoppingGold >= cost.GoldPerUnit)) return true;
            }
            if (!_world.TryGet<JobComponent>(other, out var job)) continue;
            if (job.Job == Job.Fisher && !Bag(other).Any(e => _world.TryGet<BaitComponent>(e, out var held) && held.Quantity > 0 && !_world.Has<SaleLocationComponent>(e)) &&
                Bag(merchant).Any(e => _world.TryGet<BaitComponent>(e, out var bait) && bait.Quantity > 0 &&
                    BaitRules.Find(_world, bait.BaitId) is not null && money.Amount >= BaitRules.Price(_world, bait.BaitId, _village!.BaitPrice))) return true;
            if (money.Amount >= _village!.SpicePrice && job.Job == Job.Cook && !Bag(other).Any(e => _world.Has<SpiceComponent>(e)) && Bag(merchant).Any(e => _world.Has<SpiceComponent>(e))) return true;
        }
        return false;
    }

    /// <summary>Reads inventory contents without exposing or creating another storage system.</summary>
    /// <param name="npc">The owner.</param>
    /// <returns>The existing inventory contents, or an empty sequence.</returns>
    private IEnumerable<Entity> Bag(Entity npc) => _world.TryGet<InventoryComponent>(npc, out var bag) && bag is not null ? bag.Items : Enumerable.Empty<Entity>();
}
