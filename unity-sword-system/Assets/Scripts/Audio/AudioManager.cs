using UnityEngine;
using YuJian.Core;
using YuJian.Sword;

namespace YuJian.Audio
{
    /// <summary>
    /// 音频管理器 - 空间音效编排
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("背景音乐")]
        [SerializeField] private AudioClip ambientGuqin;     // 古琴环境音
        [SerializeField] private AudioClip combatDrums;      // 战鼓

        [Header("音效")]
        [SerializeField] private AudioClip formationActivate; // 阵法激活
        [SerializeField] private AudioClip shockwaveBoom;     // 冲击波

        [Header("设置")]
        [SerializeField] [Range(0, 1)] private float musicVolume = 0.3f;
        [SerializeField] [Range(0, 1)] private float sfxVolume = 0.7f;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;

        public static AudioManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.volume = musicVolume;
            _musicSource.spatialBlend = 0f;  // 2D音乐

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.volume = sfxVolume;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Subscribe<FormationActivatedEvent>(OnFormationActivated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Unsubscribe<FormationActivatedEvent>(OnFormationActivated);
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            switch (evt.NewPhase)
            {
                case GamePhase.KaiZhen:
                    PlayMusic(ambientGuqin);
                    break;
                case GamePhase.YuJian:
                    CrossfadeMusic(combatDrums, 1f);
                    break;
                case GamePhase.PoZhen:
                    PlaySFX(shockwaveBoom);
                    break;
                case GamePhase.Idle:
                    _musicSource.Stop();
                    break;
            }
        }

        private void OnFormationActivated(FormationActivatedEvent evt)
        {
            PlaySFX(formationActivate);
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        public void CrossfadeMusic(AudioClip newClip, float duration)
        {
            if (newClip == null) return;
            // 简化实现：直接切换
            _musicSource.clip = newClip;
            _musicSource.Play();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    /// <summary>
    /// 剑体音源 - 附加到每把剑上的空间音效
    /// 运动时嗡鸣，高速时剑啸，入阵时铮鸣
    /// </summary>
    public class SwordAudioSource : MonoBehaviour
    {
        [Header("音效")]
        [SerializeField] private AudioClip humLoop;      // 低频嗡鸣
        [SerializeField] private AudioClip whooshClip;    // 高速剑啸
        [SerializeField] private AudioClip clangClip;     // 入阵铮鸣

        [Header("参数")]
        [SerializeField] private float whooshSpeedThreshold = 8f;
        [SerializeField] private float humMinSpeed = 1f;

        private AudioSource _humSource;
        private AudioSource _sfxSource;
        private SwordEntity _sword;
        private DopplerCalculator _doppler;
        private bool _wasAboveThreshold;

        private void Awake()
        {
            _sword = GetComponentInParent<SwordEntity>();

            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.loop = true;
            _humSource.spatialBlend = 1f;  // 3D空间音效
            _humSource.volume = 0f;
            _humSource.clip = humLoop;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.spatialBlend = 1f;

            _doppler = new DopplerCalculator();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SwordStateChangedEvent>(OnSwordStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SwordStateChangedEvent>(OnSwordStateChanged);
        }

        private void Update()
        {
            if (_sword == null) return;

            float speed = _sword.Speed;

            // 嗡鸣音量随速度变化
            float humVolume = Mathf.Clamp01((speed - humMinSpeed) / 5f) * 0.3f;
            _humSource.volume = humVolume;
            if (humVolume > 0 && !_humSource.isPlaying)
                _humSource.Play();
            else if (humVolume <= 0 && _humSource.isPlaying)
                _humSource.Stop();

            // 高速剑啸
            bool isAboveThreshold = speed > whooshSpeedThreshold;
            if (isAboveThreshold && !_wasAboveThreshold && whooshClip != null)
            {
                _sfxSource.PlayOneShot(whooshClip, 0.5f);
            }
            _wasAboveThreshold = isAboveThreshold;

            // 多普勒效应
            if (Camera.main != null)
            {
                float pitch = _doppler.Calculate(
                    _sword.Rb.velocity,
                    transform.position,
                    Camera.main.transform.position
                );
                _humSource.pitch = pitch;
            }
        }

        private void OnSwordStateChanged(SwordStateChangedEvent evt)
        {
            if (evt.SwordId != _sword.SwordId) return;

            if (evt.NewState == SwordState.InFormation && clangClip != null)
            {
                _sfxSource.PlayOneShot(clangClip, 0.6f);
            }
        }
    }

    /// <summary>
    /// 多普勒效应计算器
    /// pitch = basePitch * (1 + relativeVelocity / speedOfSound)
    /// </summary>
    public class DopplerCalculator
    {
        private const float SpeedOfSound = 343f;  // m/s
        private const float BasePitch = 1f;
        private const float MaxPitchShift = 0.3f;

        public float Calculate(Vector3 sourceVelocity, Vector3 sourcePos,
                                Vector3 listenerPos)
        {
            Vector3 toListener = (listenerPos - sourcePos).normalized;
            float relativeVelocity = Vector3.Dot(sourceVelocity, toListener);

            float pitchShift = Mathf.Clamp(
                relativeVelocity / SpeedOfSound,
                -MaxPitchShift, MaxPitchShift
            );

            return BasePitch + pitchShift;
        }
    }
}
