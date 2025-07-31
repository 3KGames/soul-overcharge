using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Serialization;

namespace Car.Controller
{
	[CreateAssetMenu(menuName = "ArcadeCar/NitroData")]
	public class NitroData : ScriptableObject
	{
		[Range(0f, 10f)]
		[SerializeField] private float			duration = 2f;
		[CurveRange(0f, 0f, 1f, 1f, EColor.Orange), Tooltip("How acceleration changes over time")]
		[SerializeField] private AnimationCurve ramp;
		
		[Header("Acceleration multipliers")]
		[Min(1f)]
		[SerializeField] private float miniTurboSmall 	= 3f;
		[Min(1f)]
		[SerializeField] private float miniTurboMedium	= 5f;
		[Min(1f)]
		[SerializeField] private float miniTurboLarge 	= 8f;
		
		public float 			Duration			=> duration;
		public AnimationCurve	Ramp				=> ramp;
		public float 			MiniTurboSmall  	=> miniTurboSmall;
		public float 			MiniTurboMedium 	=> miniTurboMedium;
		public float 			MiniTurboLarge  	=> miniTurboLarge;
	}
}