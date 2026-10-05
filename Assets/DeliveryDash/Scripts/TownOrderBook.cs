using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    public sealed class TownOrder
    {
        public TownAddress destination;
        public float dueAt;
        public bool late;
    }

    public sealed class TownOrderBook
    {
        public const int Capacity = 3;
        public const float ShiftDuration = 300;
        public const int LateFee = 5;
        private readonly List<TownOrder> orders = new List<TownOrder>();
        public IReadOnlyList<TownOrder> Orders => orders;
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0, ShiftDuration - Elapsed);
        public bool Finished => Remaining <= 0;
        public int Money { get; private set; }
        public int Delivered { get; private set; }
        public int LateFees { get; private set; }
        public int Batches { get; private set; }
        public void Reset()
        {
            orders.Clear(); Elapsed = 0; Money = Delivered = LateFees = Batches = 0;
        }
        public int Advance(float seconds)
        {
            if (Finished) return 0;
            Elapsed = Mathf.Min(ShiftDuration, Elapsed + Mathf.Max(0, seconds));
            int fees = 0;
            foreach (var order in orders)
                if (!order.late && Elapsed > order.dueAt)
                {
                    order.late = true; Money -= LateFee; LateFees++; fees++;
                }
            return fees;
        }
        public TownOrder Find(TownAddress address)
        {
            foreach (var order in orders) if (order.destination.Equals(address)) return order;
            return null;
        }
        public int PickupCount(int seed, TownAddress shop)
        {
            if (Finished || !TownMap.IsShop(seed, shop)) return 0;
            uint hash = EndlessRoadLayout.Hash(seed, ((long)shop.block.x << 32) ^ (uint)shop.block.y, (uint)(Batches + 400));
            return Math.Min(Capacity - orders.Count, 1 + (int)(hash % 3));
        }
        public int PickUp(int seed, TownAddress shop)
        {
            if (Finished || orders.Count >= Capacity || !TownMap.IsShop(seed, shop)) return 0;
            var candidates = new List<TownAddress>();
            for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
            {
                if (Math.Abs(x) + Math.Abs(z) > 3 || (x == 0 && z == 0)) continue;
                var node = shop.block + new Vector2Int(x, z);
                for (int side = -1; side <= 1; side += 2)
                {
                    var address = new TownAddress(node, side);
                    if (!TownMap.IsShop(seed, address) && Find(address) == null) candidates.Add(address);
                }
            }
            int count = PickupCount(seed, shop);
            Vector3 previous = TownMap.StopPosition(shop, shop.block, seed);
            float travelBudget = 0;
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int chosen = (int)(EndlessRoadLayout.Hash(seed, Batches, (uint)(i + 911)) % (uint)candidates.Count);
                TownAddress destination = candidates[chosen]; candidates.RemoveAt(chosen);
                Vector3 next = TownMap.StopPosition(destination, shop.block, seed);
                travelBudget += TownMap.RouteLength(TownMap.Network(seed).Route(previous, next, shop.block)) / 5.5f + 4;
                orders.Add(new TownOrder { destination = destination, dueAt = Elapsed + 45 + travelBudget });
                previous = next;
            }
            Batches++;
            return count;
        }
        public bool Deliver(TownAddress address)
        {
            if (Finished) return false;
            TownOrder order = Find(address);
            if (order == null) return false;
            Money += order.late ? 2 : 12;
            Delivered++; orders.Remove(order);
            return true;
        }
    }
}
