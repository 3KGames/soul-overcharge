using System;
using Car.Controller;
using Car.Gears;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using DG.Tweening;

public class TachometerController : MonoBehaviour
{
    private readonly float MAX_RPM = 9000;
    private readonly float MIN_RPM = 0;
    
    [SerializeField]  private RectTransform       arrow;
    [SerializeField]  private TextMeshProUGUI     tachometerText;
    private CarService                   _car;
    private TransmissionService          _transmission;

    [Inject]
    void Construct(CarService car, TransmissionService tr)
    {
        Debug.Log("[TachometerController] Injected OK");
        _car = car;
        _transmission = tr;
    }
    
    void Start()
    {
        tachometerText.text = "1";
        arrow.transform.rotation = Quaternion.Euler(new Vector3(0, 0, -45));
        _transmission.GearChanged += UpdateGearDisplay;
        _car.PhysicsUpdated += UpdateTachometer;
    }

    private void UpdateGearDisplay()
    {
        tachometerText.text = (_transmission.SelectedGear + 1).ToString();
        UpdateTachometer();
    }

    private void UpdateTachometer()
    {
        float rpm = _transmission.GetRpm(_car.CurrentSpeed);
        float normalizedRpm = Mathf.InverseLerp(MIN_RPM, MAX_RPM, rpm);
        normalizedRpm = Mathf.Clamp01(normalizedRpm);

        float zAngle = Mathf.Lerp(-45, -247, normalizedRpm);
        arrow.DORotate(new Vector3(0, 0, zAngle), 0.5f);
    }
}
