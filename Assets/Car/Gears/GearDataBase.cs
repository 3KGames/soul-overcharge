using UnityEngine;

namespace Car.Gears
{
    public abstract class GearDataBase : ScriptableObject
    {
        public abstract int     GearsCount  { get; }
        public abstract IGear   GetGear(int index);
    }
}