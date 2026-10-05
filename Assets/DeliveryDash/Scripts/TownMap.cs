using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    [Serializable]
    public struct TownAddress : IEquatable<TownAddress>
    {
        public Vector2Int block;
        public int side;
        public TownAddress(Vector2Int block, int side) { this.block = block; this.side = side; }
        public string Label => $"{block.x + 100}.{block.y + 100}{(side > 0 ? "E" : "W")}";
        public bool Equals(TownAddress other) => block == other.block && side == other.side;
        public override bool Equals(object other) => other is TownAddress address && Equals(address);
        public override int GetHashCode() => unchecked(block.GetHashCode() * 397 ^ side);
    }

    public static class TownMap
    {
        static readonly Dictionary<int, TownRoadNetwork> networks = new Dictionary<int, TownRoadNetwork>();
        public static TownRoadNetwork Network(int seed)
        {
            if (networks.TryGetValue(seed, out var value)) return value;
            if (networks.Count >= 4) networks.Clear();
            value = new TownRoadNetwork(seed); networks[seed] = value; return value;
        }
        static readonly Dictionary<int, Vector2> shopSettings = new Dictionary<int, Vector2>();
        public static void ConfigureShops(int seed, int districtSpan, float separation)
        { shopSettings[seed] = new Vector2(Mathf.Clamp(districtSpan, 3, 8), Mathf.Clamp(separation, 120, 450)); }
        public static int ShopSearchRadius(int seed) => shopSettings.TryGetValue(seed,out var s) ? (int)s.x * 3 : 12;
        static Vector2Int Candidate(int seed, Vector2Int sector, int span)
        {
            uint h=EndlessRoadLayout.Hash(seed,((long)sector.x<<32)^(uint)sector.y,871);
            return sector*span+new Vector2Int((int)(h%(uint)span),(int)((h>>12)%(uint)span));
        }
        public static bool HasShop(int seed, Vector2Int block)
        {
            if(block==Vector2Int.zero)return true;
            var settings=shopSettings.TryGetValue(seed,out var value)?value:new Vector2(4,250);
            int span=(int)settings.x;
            var sector=new Vector2Int(Mathf.FloorToInt(block.x/(float)span),Mathf.FloorToInt(block.y/(float)span));
            if(Candidate(seed,sector,span)!=block)return false;
            var network=Network(seed);var p=network.Address(new TownAddress(block,1)).Stop(Vector2Int.zero);
            if(Vector3.Distance(p,network.Address(new TownAddress(Vector2Int.zero,1)).Stop(Vector2Int.zero))<settings.y)return false;
            uint priority=EndlessRoadLayout.Hash(seed,((long)block.x<<32)^(uint)block.y,872);
            // Compare raw candidates, never loaded objects: revisits and load order agree.
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)
            {
                var other=Candidate(seed,sector+new Vector2Int(x,y),span);
                if(other==block)continue;
                uint rank=EndlessRoadLayout.Hash(seed,((long)other.x<<32)^(uint)other.y,872);
                if((rank<priority || (rank==priority && (other.x<block.x || other.x==block.x&&other.y<block.y))) &&
                    Vector3.Distance(p,network.Node(other,Vector2Int.zero))<settings.y+190 &&
                    Vector3.Distance(p,network.Address(new TownAddress(other,1)).Stop(Vector2Int.zero))<settings.y)return false;
            }
            return true;
        }
        public static bool IsShop(int seed, TownAddress address) => address.side > 0 && HasShop(seed, address.block);
        public static Vector3 StopPosition(TownAddress address, Vector2Int origin, int seed = 6553) => Network(seed).Address(address).Stop(origin);
        public static Vector2Int BlockAt(Vector3 position, Vector2Int origin) => Network(6553).CellAt(position, origin);
        public static List<Vector3> Route(Vector3 from, Vector3 to, int seed = 6553) => Network(seed).Route(from, to, Vector2Int.zero);
        public static float RouteLength(List<Vector3> route)
        {
            float total = 0;
            for (int i = 1; i < route.Count; i++) total += Vector3.Distance(route[i - 1], route[i]);
            return total;
        }
    }
}
