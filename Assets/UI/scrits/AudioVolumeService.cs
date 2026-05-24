using UnityEngine;
using FMODUnity;

public class AudioVolumeService
{
    private const string BusMaster = "bus:/";
    private const string BusMusic  = "bus:/Music";
    private const string BusSFX    = "bus:/SFX";

    private const string KeyMaster = "vol_master";
    private const string KeyMusic  = "vol_music";
    private const string KeySFX    = "vol_sfx";

    public float MasterVolume { get; private set; }
    public float MusicVolume  { get; private set; }
    public float SFXVolume    { get; private set; }

    public AudioVolumeService()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        MusicVolume  = PlayerPrefs.GetFloat(KeyMusic,  1f);
        SFXVolume    = PlayerPrefs.GetFloat(KeySFX,    1f);
        Apply();
    }

    public void SetMaster(float value)
    {
        MasterVolume = value;
        SetBusVolume(BusMaster, value);
        PlayerPrefs.SetFloat(KeyMaster, value);
    }

    public void SetMusic(float value)
    {
        MusicVolume = value;
        SetBusVolume(BusMusic, value);
        PlayerPrefs.SetFloat(KeyMusic, value);
    }

    public void SetSFX(float value)
    {
        SFXVolume = value;
        SetBusVolume(BusSFX, value);
        PlayerPrefs.SetFloat(KeySFX, value);
    }

    private void Apply()
    {
        SetBusVolume(BusMaster, MasterVolume);
        SetBusVolume(BusMusic,  MusicVolume);
        SetBusVolume(BusSFX,    SFXVolume);
    }

    private void SetBusVolume(string busPath, float volume)
    {
        try
        {
            var bus = RuntimeManager.GetBus(busPath);
            bus.setVolume(volume);
        }
        catch
        {
            Debug.LogWarning($"[AudioVolumeService] Bus не найден: {busPath}");
        }
    }
}