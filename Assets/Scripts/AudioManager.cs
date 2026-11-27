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
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

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

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureAudioSources();
    }

    private void Start()
    {
        if (mainBgm != null)
        {
            PlayBGM(mainBgm, bgmVolume);
        }
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

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            bgmSource.volume = Mathf.Clamp01(volume);
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.volume = Mathf.Clamp01(volume);
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
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
