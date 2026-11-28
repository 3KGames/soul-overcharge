using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Game.Enemies.Audio
{
    public sealed class EnemyAudioFmodAdapter : MonoBehaviour
    {
        [SerializeField] private EventReference _engineEvent;
        [SerializeField] private EventReference _gearShiftEvent;

        private EventInstance _engineInstance;
        private PARAMETER_ID _rpmId;
        private PARAMETER_ID _loadId;
        private PARAMETER_ID _slipId;
        private PARAMETER_ID _gearId;
        private bool _hasRpm;
        private bool _hasLoad;
        private bool _hasSlip;
        private bool _hasGear;
        private bool _isPaused;

        public bool IsPaused => _isPaused;

        private void Awake()
        {
            if (_engineEvent.IsNull) return;

            _engineInstance = RuntimeManager.CreateInstance(_engineEvent);
            RuntimeManager.AttachInstanceToGameObject(_engineInstance, gameObject);
            CacheParameterIds();
            _engineInstance.start();
        }

        private void OnDestroy()
        {
            if (_engineInstance.isValid())
            {
                _engineInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _engineInstance.release();
            }
        }

        public void SetPaused(bool paused)
        {
            if (_isPaused == paused || !_engineInstance.isValid()) return;
            _isPaused = paused;
            _engineInstance.setPaused(paused);
        }

        public void UpdateEngineParameters(float rpm, float load, float slipNorm, int gear)
        {
            if (_isPaused || !_engineInstance.isValid()) return;

            if (_hasRpm) _engineInstance.setParameterByID(_rpmId, rpm);
            if (_hasLoad) _engineInstance.setParameterByID(_loadId, load);
            if (_hasSlip) _engineInstance.setParameterByID(_slipId, slipNorm);
            if (_hasGear) _engineInstance.setParameterByID(_gearId, gear);
        }

        public void PlayGearShiftOneShot()
        {
            if (_isPaused || _gearShiftEvent.IsNull) return;
            RuntimeManager.PlayOneShotAttached(_gearShiftEvent, gameObject);
        }

        private void CacheParameterIds()
        {
            if (!_engineInstance.isValid()) return;
            if (_engineInstance.getDescription(out var desc) != RESULT.OK) return;

            _hasRpm = TryGetParamId(desc, "RPM", out _rpmId);
            _hasLoad = TryGetParamId(desc, "Load", out _loadId);
            _hasSlip = TryGetParamId(desc, "Slip", out _slipId);
            _hasGear = TryGetParamId(desc, "Gear", out _gearId);
        }

        private static bool TryGetParamId(EventDescription desc, string paramName, out PARAMETER_ID id)
        {
            if (desc.getParameterDescriptionByName(paramName, out var paramDesc) == RESULT.OK)
            {
                id = paramDesc.id;
                return true;
            }

            id = default;
            return false;
        }
    }
}