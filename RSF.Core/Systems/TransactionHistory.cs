using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Records completed payments for debugging without executing commands, moving items, or altering gold.</summary>
public static class TransactionHistory
{
    /// <summary>Snapshots a successful item transfer after both balances have been updated.</summary>
    /// <param name="world">The trading world.</param>
    /// <param name="buyer">The payer.</param>
    /// <param name="seller">The receiver.</param>
    /// <param name="item">The actual transferred item, including a newly split stack.</param>
    /// <param name="quantity">Actual purchased units.</param>
    /// <param name="price">Actual gold per purchased unit.</param>
    /// <param name="buyerBefore">Buyer balance before payment.</param>
    /// <param name="sellerBefore">Seller balance before payment.</param>
    public static void Purchase(World world, Entity buyer, Entity seller, Entity item, int quantity, int price, int buyerBefore, int sellerBefore)
    {
        var policy = GameSettings.Find(world)?.Economy.TransactionHistory ?? new TransactionHistorySettings();
        if (!policy.Enabled) return;
        string name = Name(world, item);
        if (world.TryGet<MealComponent>(item, out var meal)) name += $" ({meal.Quality})";
        bool meat = world.Has<PlantProduceComponent>(item) && FishMeat.Grams(world, item) > 0;
        int? weight = world.TryGet<FishComponent>(item, out var fish) ? fish.WeightGrams : null;
        int total = (int)((long)quantity * price); // TradeSystem has already validated the payment against integer gold limits.
        Record(world, buyer, seller, new TransactionRecord(FoodRules.Now(world), TransactionKind.Purchase,
            buyer.Id, Name(world, buyer), seller.Id, Name(world, seller), name, quantity, meat ? "g" : "unit(s)", weight,
            price, total, buyerBefore, Balance(world, buyer), sellerBefore, Balance(world, seller)), policy.MaximumEntries);
    }

    /// <summary>Snapshots one successful daily rent transfer, including automatic renewals.</summary>
    /// <param name="world">The inn world.</param>
    /// <param name="guest">The payer.</param>
    /// <param name="owner">The inn owner receiving rent.</param>
    /// <param name="inn">The rented building.</param>
    /// <param name="at">The shared clock's payment minute.</param>
    /// <param name="price">The actual daily rent.</param>
    /// <param name="guestBefore">Guest balance before payment.</param>
    /// <param name="ownerBefore">Owner balance before payment.</param>
    public static void Rent(World world, Entity guest, Entity owner, Entity inn, GameMinute at, int price, int guestBefore, int ownerBefore)
    {
        var policy = GameSettings.Find(world)?.Economy.TransactionHistory ?? new TransactionHistorySettings();
        if (!policy.Enabled) return;
        Record(world, guest, owner, new TransactionRecord(at, TransactionKind.Rent,
            guest.Id, Name(world, guest), owner.Id, Name(world, owner), Name(world, inn), 1, "day(s)", null,
            price, price, guestBefore, Balance(world, guest), ownerBefore, Balance(world, owner)), policy.MaximumEntries);
    }

    /// <summary>Shares one immutable record between both NPCs and the village archive, which survives merchant departure.</summary>
    /// <param name="world">The payment world.</param>
    /// <param name="payer">The existing payer entity.</param>
    /// <param name="receiver">The existing receiver entity.</param>
    /// <param name="record">The completed payment snapshot.</param>
    /// <param name="limit">Maximum retained entries in each history.</param>
    private static void Record(World world, Entity payer, Entity receiver, TransactionRecord record, int limit)
    {
        Append(world, payer, record, limit);
        Append(world, receiver, record, limit);
        if (!world.TryGet<VillageResidentComponent>(payer, out var resident) || resident is null) return;
        foreach (Entity village in world.GetEntitiesWith<VillageComponent>())
            if (world.TryGet<VillageComponent>(village, out var data) && data == resident.Village)
            { Append(world, village, record, limit); break; }
    }

    /// <summary>Appends one record, updates lifetime totals, and discards only the oldest diagnostic entries beyond the limit.</summary>
    /// <param name="world">The component world.</param>
    /// <param name="owner">The NPC or village owning a history.</param>
    /// <param name="record">The immutable payment snapshot.</param>
    /// <param name="limit">Positive maximum retained entries.</param>
    private static void Append(World world, Entity owner, TransactionRecord record, int limit)
    {
        if (!world.TryGet<TransactionHistoryComponent>(owner, out var history) || history is null)
        { history = new TransactionHistoryComponent(owner.Id); world.Add(owner, history); }
        history.Entries.Enqueue(record);
        history.TotalRecorded++;
        if (owner.Id == record.PayerId) history.GoldSpent += record.TotalGold;
        if (owner.Id == record.ReceiverId) history.GoldReceived += record.TotalGold;
        while (history.Entries.Count > limit) history.Entries.Dequeue();
    }

    /// <summary>Copies a readable name while the referenced entity still exists.</summary>
    /// <param name="world">The entity world.</param>
    /// <param name="entity">An actor, item, or building.</param>
    /// <returns>The saved name, or the existing ID when no name is available.</returns>
    private static string Name(World world, Entity entity) => world.TryGet<NameComponent>(entity, out var name) ? name.Value
        : world.TryGet<ItemComponent>(entity, out var item) ? item.Name : entity.Id.ToString();

    /// <summary>Reads the balance after the existing payment system has completed its transfer.</summary>
    /// <param name="world">The payment world.</param>
    /// <param name="npc">The payer or receiver.</param>
    /// <returns>The current gold balance.</returns>
    private static int Balance(World world, Entity npc) => world.TryGet<GoldComponent>(npc, out var gold) ? gold.Amount : 0;
}
