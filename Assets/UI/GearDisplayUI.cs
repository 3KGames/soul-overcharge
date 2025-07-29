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

        private void Start()
        {
            car.OnGearChanged += UpdateGearDisplay;
            UpdateGearDisplay(car.CurrentGearIndex);

            car.OnSpeedChanged += UpdateSpeedDisplay;
            UpdateSpeedDisplay(car.CurrentSpeed);
        }

        private void OnDestroy() => car.OnGearChanged -= UpdateGearDisplay;

        private void UpdateGearDisplay(int gearIndex) => gearText.text = (gearIndex + 1).ToString();
        private void UpdateSpeedDisplay(int speed) => speedText.text = speed.ToString();
    }
}