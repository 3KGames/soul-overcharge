using UnityEngine;

namespace Game.Level.Runtime.Config
{
    [CreateAssetMenu(fileName = "RoadGridSettings", menuName = "Road Generator/Grid Settings")]
    public class RoadGridSettings : ScriptableObject
    {
        [Header("Grid")]
        [Min(0.01f)] public float cellSize = 40f;
        [Min(1)] public int maxLanes = 11;
    }
}