using UnityEngine;
using UnityEngine.UI;
using Car.Gears;
using VContainer;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private GameObject _modalRoot;
    [SerializeField] private Toggle _autoTransmissionToggle;
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;

    private TransmissionService _transmissionService;
    private AudioVolumeService  _audioVolumeService;

    [Inject]
    public void Construct(TransmissionService transmissionService, AudioVolumeService audioVolumeService)
    {
        _transmissionService = transmissionService;
        _audioVolumeService  = audioVolumeService;
    }

    private void Start()
    {
        _autoTransmissionToggle.isOn = _transmissionService.IsAutoTransmission;
        _autoTransmissionToggle.onValueChanged.AddListener(OnTransmissionChanged);

        _masterSlider.value = _audioVolumeService.MasterVolume;
        _musicSlider.value  = _audioVolumeService.MusicVolume;
        _sfxSlider.value    = _audioVolumeService.SFXVolume;

        _masterSlider.onValueChanged.AddListener(_audioVolumeService.SetMaster);
        _musicSlider.onValueChanged.AddListener(_audioVolumeService.SetMusic);
        _sfxSlider.onValueChanged.AddListener(_audioVolumeService.SetSFX);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && _modalRoot.activeSelf)
            CloseModal();
    }

    private void OnTransmissionChanged(bool isAuto)
    {
        if (_transmissionService == null) return;
        _transmissionService.IsAutoTransmission = isAuto;
    }

    public void OpenModal()
    {
        _modalRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void CloseModal()
    {
        _modalRoot.SetActive(false);
        Time.timeScale = 1f;
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        _autoTransmissionToggle.onValueChanged.RemoveListener(OnTransmissionChanged);
        _masterSlider.onValueChanged.RemoveListener(_audioVolumeService.SetMaster);
        _musicSlider.onValueChanged.RemoveListener(_audioVolumeService.SetMusic);
        _sfxSlider.onValueChanged.RemoveListener(_audioVolumeService.SetSFX);
    }
}