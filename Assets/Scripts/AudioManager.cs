using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("BGM")]
    [SerializeField] private AudioClip mainBgm;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;

    [Header("Common SFX")]
    [SerializeField] private AudioClip warningSfx;
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField] private AudioClip hitSfx;
    [SerializeField] private AudioClip gameOverSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private Coroutine bgmFadeRoutine;
    private AudioClip pendingBgmClip;
    private float targetBgmVolume = 1f;

    public AudioSource BgmSource => bgmSource;
    public AudioSource SfxSource => sfxSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        targetBgmVolume = Mathf.Clamp01(bgmVolume);

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureAudioSources();
        bgmSource.playOnAwake = false;
        bgmSource.Stop();
    }

    private void OnDisable()
    {
        bgmFadeRoutine = null;
        pendingBgmClip = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSources();
        StopBgmFadeRoutine();

        float clampedVolume = Mathf.Clamp01(volume);
        targetBgmVolume = clampedVolume;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            bgmSource.volume = clampedVolume;
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.volume = clampedVolume;
        bgmSource.Play();
    }

    public void PlayBgm(AudioClip clip, float fadeDuration = 0.4f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSources();

        if (bgmFadeRoutine != null && pendingBgmClip == clip)
        {
            return;
        }

        if (bgmFadeRoutine == null && bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        float restoreVolume = Mathf.Clamp01(targetBgmVolume);

        if (fadeDuration <= 0f || !isActiveAndEnabled)
        {
            StopBgmFadeRoutine();
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = restoreVolume;
            bgmSource.Play();
            return;
        }

        StopBgmFadeRoutine();
        pendingBgmClip = clip;
        bgmFadeRoutine = StartCoroutine(FadeBgmRoutine(clip, fadeDuration, restoreVolume));
    }

    public void RestoreMainBgm(float fadeDuration = 0.4f)
    {
        PlayBgm(mainBgm, fadeDuration);
    }

    public void ResetBgmOnRestart(float fadeDuration = 0.2f)
    {
        if (mainBgm == null)
        {
            return;
        }

        EnsureAudioSources();

        bool isPlayingMainBgm = bgmFadeRoutine == null && bgmSource.clip == mainBgm && bgmSource.isPlaying;
        bool isFadingToMainBgm = bgmFadeRoutine != null && pendingBgmClip == mainBgm;
        if (isPlayingMainBgm || isFadingToMainBgm)
        {
            return;
        }

        RestoreMainBgm(fadeDuration);
    }

    public void StopBGM()
    {
        StopBgm(0f);
    }

    public void StopBgm(float fadeDuration = 0f)
    {
        StopBgmFadeRoutine();

        float defaultVolume = Mathf.Clamp01(bgmVolume);
        targetBgmVolume = defaultVolume;

        if (bgmSource == null)
        {
            return;
        }

        if (fadeDuration <= 0f || !isActiveAndEnabled || !bgmSource.isPlaying)
        {
            bgmSource.Stop();
            bgmSource.volume = defaultVolume;
            return;
        }

        bgmFadeRoutine = StartCoroutine(FadeOutAndStopBgmRoutine(fadeDuration, defaultVolume));
    }

    public void RestartMainBgm()
    {
        StopBgmFadeRoutine();

        if (mainBgm == null)
        {
            return;
        }

        EnsureAudioSources();

        float defaultVolume = Mathf.Clamp01(bgmVolume);
        targetBgmVolume = defaultVolume;

        bgmSource.Stop();
        bgmSource.clip = mainBgm;
        bgmSource.loop = true;
        bgmSource.volume = defaultVolume;
        bgmSource.time = 0f;
        bgmSource.Play();
    }

    private IEnumerator FadeOutAndStopBgmRoutine(float fadeDuration, float defaultVolume)
    {
        if (bgmSource != null && bgmSource.isPlaying && bgmSource.volume > 0f)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
        }

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.volume = defaultVolume;
        }

        targetBgmVolume = defaultVolume;
        pendingBgmClip = null;
        bgmFadeRoutine = null;
    }

    private IEnumerator FadeBgmRoutine(AudioClip nextClip, float fadeDuration, float restoreVolume)
    {
        float halfDuration = fadeDuration * 0.5f;

        if (bgmSource.isPlaying && bgmSource.volume > 0f)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = halfDuration > 0f ? Mathf.Clamp01(elapsed / halfDuration) : 1f;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
        }

        bgmSource.volume = 0f;
        bgmSource.clip = nextClip;
        bgmSource.loop = true;
        bgmSource.Play();

        float fadeInElapsed = 0f;
        while (fadeInElapsed < halfDuration)
        {
            fadeInElapsed += Time.unscaledDeltaTime;
            float t = halfDuration > 0f ? Mathf.Clamp01(fadeInElapsed / halfDuration) : 1f;
            bgmSource.volume = Mathf.Lerp(0f, restoreVolume, t);
            yield return null;
        }

        bgmSource.volume = restoreVolume;
        pendingBgmClip = null;
        bgmFadeRoutine = null;
    }

    private void StopBgmFadeRoutine()
    {
        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }

        pendingBgmClip = null;
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSources();
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * Mathf.Clamp01(sfxVolume));
    }

    public void PlayWarningSFX(float volume = 1f)
    {
        PlaySFX(warningSfx, volume);
    }

    public void PlayJumpSFX(float volume = 1f)
    {
        PlaySFX(jumpSfx, volume);
    }

    public void PlayHitSFX(float volume = 1f)
    {
        PlaySFX(hitSfx, volume);
    }

    public void PlayGameOverSfx(float volume = 1f)
    {
        if (gameOverSfx == null)
        {
            return;
        }

        PlaySFX(gameOverSfx, volume);
    }

    private void EnsureAudioSources()
    {
        AudioSource[] existingSources = GetComponents<AudioSource>();

        if (bgmSource == null)
        {
            for (int i = 0; i < existingSources.Length; i++)
            {
                if (existingSources[i] != null && existingSources[i] != sfxSource)
                {
                    bgmSource = existingSources[i];
                    break;
                }
            }

            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (sfxSource == null || sfxSource == bgmSource)
        {
            sfxSource = null;
            for (int i = 0; i < existingSources.Length; i++)
            {
                if (existingSources[i] != null && existingSources[i] != bgmSource)
                {
                    sfxSource = existingSources[i];
                    break;
                }
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }
}
