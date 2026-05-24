using UnityEngine;
using FMODUnity;
using FMOD.Studio;

namespace Enemies.Biker.Runtime
{
    public class BikerEngineSound : MonoBehaviour
    {
		[SerializeField]
		private BikerContext bikerContext;
		
        [Header("FMOD Настройки")]
        public EventReference EngineEvent;
        public string ParameterName = "Acceleration";

        [Header("Настройки влияния")]
        [Tooltip("Насколько сильно текущая скорость влияет на звук (базовые обороты)")]
        public float SpeedMultiplier = 0.02f;
        
        [Tooltip("Насколько сильно ускорение (нажатие на газ) добавляет агрессии звуку")]
        public float AccelerationMultiplier = 0.05f;

        [Header("Ограничения параметра в FMOD")]
        public float MinParameterValue = 0f;
        public float MaxParameterValue = 1f;

        [Header("Сглаживание (защита от скачков физики)")]
        public float SmoothSpeed = 5f;

        private EventInstance _engineInstance;
        private float _currentParamValue;

        private void Start()
        {
            if (!EngineEvent.IsNull)
            {
                
                _engineInstance = RuntimeManager.CreateInstance(EngineEvent);
                
                
                RuntimeManager.AttachInstanceToGameObject(_engineInstance, transform, GetComponent<Rigidbody>());
                
                _engineInstance.start();
            }
            else
            {
                Debug.LogWarning("[BikerEngineSound] Не назначен EventReference для звука двигателя!");
            }
        }

        private void Update()
        {
            if (!_engineInstance.isValid()) return;

            float speedFactor = Mathf.Abs(bikerContext.CurrentSpeed) * SpeedMultiplier;

            float accelFactor = Mathf.Max(0f, bikerContext.CurrentAcceleration) * AccelerationMultiplier;

            float targetValue = speedFactor + accelFactor;
            targetValue = Mathf.Clamp(targetValue, MinParameterValue, MaxParameterValue);

            _currentParamValue = Mathf.Lerp(_currentParamValue, targetValue, Time.deltaTime * SmoothSpeed);

            _engineInstance.setParameterByName(ParameterName, _currentParamValue);
        }

        private void OnDestroy()
        {
            if (_engineInstance.isValid())
            {
                _engineInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _engineInstance.release();
            }
        }
    }
}