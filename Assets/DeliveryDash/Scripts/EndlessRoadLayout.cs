using System;
using UnityEngine;

namespace DeliveryDash
{
    // Pure, indexed layout: visiting a section in a different order never changes it.
    public static class EndlessRoadLayout
    {
        public const float Length = 48f;
        public const float Grade = 0.06f;
        public enum Beat { Cruise, Bend, Market, Median }

        public static uint Hash(int seed, long index, uint salt = 0)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)index * 374761393u ^ (uint)(index >> 32) * 668265263u ^ salt;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Unit(int seed, long index, uint salt) => (Hash(seed, index, salt) & 0xffffff) / 16777215f;
        public static float Center(int seed, long boundary) => boundary <= 1 ? 0f : (Unit(seed, boundary, 17) - .5f) * 7f;
        public static float Width(int seed, long boundary) => boundary <= 1 ? 6.15f : 4.8f + Unit(seed, boundary, 91) * 1.7f;
        public static Beat Kind(int seed, long index)
        {
            // A calm section between challenges gives time to recover.
            if (index <= 1 || index % 2 == 0) return Beat.Cruise;
            return (Beat)(1 + Hash(seed, index, 57) % 3);
        }

        public static Vector3 Sample(int seed, double station, double origin, out float halfWidth)
        {
            long index = (long)Math.Floor(station / Length);
            float t = (float)((station - index * (double)Length) / Length);
            float blend = t * t * (3f - 2f * t);
            float center = Mathf.Lerp(Center(seed, index), Center(seed, index + 1), blend);
            halfWidth = Mathf.Lerp(Width(seed, index), Width(seed, index + 1), blend);
            // Derivative stays negative: -0.06 +/- 0.0125, including at joins.
            double ripple = .3 * Math.Sin((station % (Math.PI * 48)) / 24);
            return new Vector3(center, (float)(-Grade * (station - origin) + ripple), (float)(station - origin));
        }
    }
}
