using Car.Controller;
using UnityEngine;
using TMPro;

namespace UI
{
    public class GearDisplayUI : MonoBehaviour
    {
        [SerializeField] private	CarController		car;
        [SerializeField] private	TextMeshProUGUI		gearText;
        [SerializeField] private	TextMeshProUGUI		speedText;
		[SerializeField] private	float				speedCoefficient = 1f;

		private void Update()
		{
			UpdateSpeedDisplay(car.RB.linearVelocity.magnitude);
		}

		private void UpdateSpeedDisplay(float speed) => speedText.text = ((int)(speed * speedCoefficient * 3.6f)).ToString();
    }
}