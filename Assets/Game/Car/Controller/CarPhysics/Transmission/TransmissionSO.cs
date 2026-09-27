using UnityEngine;

namespace Car.Controller.CarPhysics.Transmission
{
	[CreateAssetMenu(fileName = "Transmission", menuName = "Car/Transmission", order = 1)]
	public class TransmissionSO : ScriptableObject
	{
		[Header("Gears")]
		[Tooltip("Передаточные числа. Итоговое отношение = Gears[i] * FinalGear")]
		[SerializeField] private float[] gears = { 3.75f, 2.45f, 1.78f, 1.32f, 1.0f };

		[Tooltip("Главная передача. Итоговое отношение = Gears[i] * FinalGear")]
		[Range(0.5f, 10f)] [SerializeField] private float finalGear = 3.9f;

		[Tooltip("КПД. Домножается на момент после сцепления")]
		[Range(0.1f, 1f)] [SerializeField] private float efficiency = 0.9f;

		[Header("Clutch")]
		[Tooltip("ClutchMax = PeakTorque * ClutchCoef. Больше — меньше пробуксовка, " +
		         "меньше — машина недотягивает")]
		[Range(0.1f, 10f)] [SerializeField] private float clutchCoef = 1.5f;

		[Tooltip("Значение рассогласования в рад/с, при котором сцепление выходит на полный момент. " +
		         "ClutchProportion = ClutchMax / ClutchError, Н*м на рад/с. " +
		         "16.7 рад/с = 160 об/мин. Меньше — жёстче сцепление. " +
		         "Устойчивость требует: dt < 2 * Inertia / ClutchProportion")]
		[Range(0.5f, 200f)] [SerializeField] private float clutchError = 16.7f;

		[Header("Auto shift")]
		[Tooltip("Порог повышения передачи, об/мин")]
		[Range(1000f, 20000f)] [SerializeField] private float shiftUpRPM = 6000f;

		[Tooltip("Порог понижения передачи, об/мин. Должен быть заметно ниже ShiftUpRPM, " +
		         "иначе коробка будет дёргаться на границе")]
		[Range(500f, 20000f)] [SerializeField] private float shiftDownRPM = 2800f;

		[Tooltip("Пауза между переключениями, сек")]
		[Min(0f)] [SerializeField] private float shiftCooldownTime = 0.35f;

		[Header("Clutch engagement")]
		[Tooltip("Время нарастания сцепления при трогании с места, сек")]
		[Min(0f)] [SerializeField] private float clutchEngageTime = 0.4f;

		[Tooltip("Скорость, ниже которой считаем, что трогаемся, м/с")]
		[Min(0f)] [SerializeField] private float launchSpeed = 2f;

		public float[] Gears      => gears;
		public float   FinalGear  => finalGear;
		public float   Efficiency => efficiency;
		public float   ClutchCoef => clutchCoef;
		public float   ClutchError => clutchError;

		public float ShiftUpRPM         => shiftUpRPM;
		public float ShiftDownRPM       => shiftDownRPM;
		public float ShiftCooldownTime  => shiftCooldownTime;
		public float ClutchEngageTime   => clutchEngageTime;
		public float LaunchSpeed        => launchSpeed;
	}
}
