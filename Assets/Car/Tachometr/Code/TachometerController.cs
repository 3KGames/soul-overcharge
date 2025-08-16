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
    [SerializeField]  private CarController       car;
    [Inject]          private GearDataRpm         _gearDataRpm;

    private int _prevGear   = 1;
    private int _prevSpeed  = 0;

    void Start()
    {
        tachometerText.text = _prevGear.ToString();
        arrow.transform.rotation = Quaternion.Euler(new Vector3(0, 0, -45));
        car.OnGearChanged += UpdateGearDisplay;
        car.OnSpeedChanged += UpdateTachometer;
    }

    private void UpdateGearDisplay(int gear)
    {
        _prevGear = gear;
        tachometerText.text = (gear + 1).ToString();
        UpdateTachometer(_prevSpeed);
    }

    private void UpdateTachometer(int speed)
    {
        _prevSpeed = speed;
        var gear = _gearDataRpm.GetGear(_prevGear);
        
        float rpm = gear.EvaluateRpm(speed);
        float normalizedRpm = Mathf.InverseLerp(MIN_RPM, MAX_RPM, rpm);
        normalizedRpm = Mathf.Clamp01(normalizedRpm);

        float zAngle = Mathf.Lerp(-45, -247, normalizedRpm);
        arrow.DORotate(new Vector3(0, 0, zAngle), 0.5f);
    }
}
