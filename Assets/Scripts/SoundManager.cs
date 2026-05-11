using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// 인스펙터에서 이름과 오디오 클립을 넣기 위한 클래스
[System.Serializable]
public class Sound
{
    public string soundName; // 예: "Click", "PlaceStone", "Skill_Sanctify"
    public AudioClip clip;
}

public class SoundManager : Singleton<SoundManager>
{
    [Header("오디오 믹서")]
    public AudioMixer masterMixer; 

    [Header("오디오 소스 (재생기)")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("사운드 리스트 (인스펙터용)")]
    public Sound[] bgmList;
    public Sound[] sfxList;

    // 코드로 빠르게 찾기 위한 딕셔너리
    private Dictionary<string, AudioClip> bgmDictionary = new Dictionary<string, AudioClip>();
    private Dictionary<string, AudioClip> sfxDictionary = new Dictionary<string, AudioClip>();

    protected override void Awake()
    {
        base.Awake(); // 여기서 싱글톤 & DontDestroyOnLoad 처리가 알아서 됩니다.

        // 씬 파괴 방지 로직을 통과한 진짜 원본만 아래 초기화를 진행합니다.
        if (Instance == this)
        {
            InitializeDictionaries();
        }
    }

    private void InitializeDictionaries()
    {
        // 배열로 받은 데이터를 딕셔너리에 넣어 이름으로 바로 찾을 수 있게 세팅
        foreach (Sound bgm in bgmList)
            if (!bgmDictionary.ContainsKey(bgm.soundName)) bgmDictionary.Add(bgm.soundName, bgm.clip);

        foreach (Sound sfx in sfxList)
            if (!sfxDictionary.ContainsKey(sfx.soundName)) sfxDictionary.Add(sfx.soundName, sfx.clip);
    }

    // ==========================================
    // 여기서부터 외부에서 부를 함수들입니다.
    // ==========================================

    /// <summary>
    /// 배경음악 재생 (기존 브금은 멈추고 새 브금 루프 재생)
    /// </summary>
    public void PlayBGM(string name)
    {
        if (bgmDictionary.TryGetValue(name, out AudioClip clip))
        {
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }
        else
        {
            Debug.LogWarning($"[SoundManager] '{name}' BGM을 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 효과음 재생 (여러 효과음이 겹쳐서 재생됨)
    /// </summary>
    public void PlaySFX(string name)
    {
        if (sfxDictionary.TryGetValue(name, out AudioClip clip))
        {
            // PlayOneShot을 쓰면 소리가 끊기지 않고 겹쳐서 자연스럽게 재생됩니다.
            sfxSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"[SoundManager] '{name}' SFX를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 배경음악 멈춤
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
    }


    // ==========================================
    // 볼륨 조절 함수 업데이트 (데시벨 변환 적용)
    // ==========================================

    /// <summary>
    /// BGM 볼륨 조절 (UI 슬라이더의 0.0001 ~ 1.0 값을 받아서 데시벨로 변환)
    /// ex) 
    ///     bgmSlider.minValue = 0.0001f;
    ///     bgmSlider.maxValue = 1f;
    ///     bgmSlider.onValueChanged.AddListener(SoundManager.Instance.SetBGMVolume);
    /// </summary>
    public void SetBGMVolume(float sliderValue)
    {
        // 슬라이더 최소값을 0으로 하면 Log 연산 시 에러(-Infinity)가 나므로 0.0001 같은 0에 가까운 값을 줍니다.
        float volume = Mathf.Log10(sliderValue) * 20;
        masterMixer.SetFloat("BGMVolume", volume);
    }

    /// <summary>
    /// SFX 볼륨 조절
    /// </summary>
    public void SetSFXVolume(float sliderValue)
    {
        float volume = Mathf.Log10(sliderValue) * 20;
        masterMixer.SetFloat("SFXVolume", volume);
    }
    //sfx 반복 재생 (예: 스킬 효과음이 여러 번 겹쳐서 재생되어야 할 때)
    public void PlaySFXRepeat(string name, int count)
    {
        StartCoroutine(PlaySFXRepeatRoutine(name, count));
    }
    // 효과음 반복 재생 코루틴
    private IEnumerator PlaySFXRepeatRoutine(string name, int count)
    {
        if (!sfxDictionary.TryGetValue(name, out AudioClip clip)) yield break;

        for (int i = 0; i < count; i++)
        {
            sfxSource.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);
        }
    }
}