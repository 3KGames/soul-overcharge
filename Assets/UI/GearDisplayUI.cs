using Car.Controller;
using UnityEngine;
using TMPro;

namespace UI
{
    public class GearDisplayUI : MonoBehaviour
    {
        [SerializeField] private CarController car;
        [SerializeField] private TextMeshProUGUI gearText;

        private void Start()
        {
            car.OnGearChanged += UpdateDisplay;
            UpdateDisplay(car.CurrentGearIndex);
        }

        private void OnDestroy() => car.OnGearChanged -= UpdateDisplay;

        private void UpdateDisplay(int gearIndex) => gearText.text = (gearIndex + 1).ToString();
    }
}