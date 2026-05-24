using NaughtyAttributes;
using UnityEngine;

namespace Car.Health.Data
{
    [CreateAssetMenu(menuName = "ArcadeCar/HealthPickupData")]
    public class HealthPickupData : ScriptableObject
    {
        [Min(0f)]
        [SerializeField] private float healAmount = 20f;
        public float      HealAmount   => healAmount;
    }
}
