using UnityEngine;
using YuJian.Core;
using YuJian.Sword;

namespace YuJian.VFX
{
    /// <summary>
    /// 剑体拖尾控制器 - 根据速度动态调整TrailRenderer
    /// 低速=细蓝流光，高速=宽白残影
    /// </summary>
    public class SwordTrailController : MonoBehaviour
    {
        [Header("拖尾参数")]
        [SerializeField] private float minWidth = 0.02f;
        [SerializeField] private float maxWidth = 0.3f;
        [SerializeField] private float speedThreshold = 5f;   // 速度阈值
        [SerializeField] private float maxSpeedRef = 20f;     // 最大参考速度

        [Header("颜色")]
        [SerializeField] private Color lowSpeedColor = new Color(0.3f, 0.6f, 1f, 0.6f);
        [SerializeField] private Color highSpeedColor = new Color(1f, 1f, 1f, 0.9f);

        private TrailRenderer _trail;
        private SwordEntity _sword;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            _sword = GetComponentInParent<SwordEntity>();
        }

        private void Update()
        {
            if (_trail == null || _sword == null) return;

            float speed = _sword.Speed;
            float t = Mathf.Clamp01(speed / maxSpeedRef);

            // 宽度随速度增加
            float width = Mathf.Lerp(minWidth, maxWidth, t);
            _trail.startWidth = width;
            _trail.endWidth = width * 0.1f;

            // 颜色随速度变化
            Color color = Color.Lerp(lowSpeedColor, highSpeedColor, t);
            _trail.startColor = color;
            _trail.endColor = new Color(color.r, color.g, color.b, 0f);

            // 低速时隐藏拖尾
            _trail.emitting = speed > 0.5f;
        }
    }

    /// <summary>
    /// 剑气波发射器 - 推/拉手势触发方向性粒子
    /// </summary>
    public class EnergyWaveEmitter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem waveParticles;
        [SerializeField] private float emitDuration = 0.5f;

        private float _emitTimer;

        private void OnEnable()
        {
            EventBus.Subscribe<GestureDetectedEvent>(OnGesture);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GestureDetectedEvent>(OnGesture);
        }

        private void OnGesture(GestureDetectedEvent evt)
        {
            if (evt.Type == GestureType.Push || evt.Type == GestureType.Launch)
            {
                Emit(Vector3.forward);
            }
        }

        public void Emit(Vector3 direction)
        {
            if (waveParticles == null) return;

            transform.rotation = Quaternion.LookRotation(direction);
            waveParticles.Play();
            _emitTimer = emitDuration;
        }

        private void Update()
        {
            if (_emitTimer > 0)
            {
                _emitTimer -= Time.deltaTime;
                if (_emitTimer <= 0 && waveParticles != null)
                    waveParticles.Stop();
            }
        }
    }

    /// <summary>
    /// 阵法连线发光效果 - LineRenderer连接阵中飞剑
    /// </summary>
    public class FormationGlowEffect : MonoBehaviour
    {
        [Header("连线参数")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float minAlpha = 0.2f;
        [SerializeField] private float maxAlpha = 0.8f;

        private SwordPool _swordPool;
        private float _pulsePhase;

        private void Start()
        {
            _swordPool = FindObjectOfType<SwordPool>();
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();
        }

        private void Update()
        {
            if (_swordPool == null || lineRenderer == null) return;

            var swords = _swordPool.ActiveSwords;
            if (swords.Count < 2)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            // 更新连线位置（闭合环形）
            lineRenderer.positionCount = swords.Count + 1;
            for (int i = 0; i < swords.Count; i++)
            {
                lineRenderer.SetPosition(i, swords[i].transform.position);
            }
            lineRenderer.SetPosition(swords.Count, swords[0].transform.position);

            // 脉动效果
            _pulsePhase += Time.deltaTime * pulseSpeed;
            float alpha = Mathf.Lerp(minAlpha, maxAlpha,
                (Mathf.Sin(_pulsePhase) + 1f) * 0.5f);

            Color c = lineRenderer.startColor;
            c.a = alpha;
            lineRenderer.startColor = c;
            lineRenderer.endColor = c;
        }
    }

    /// <summary>
    /// 冲击波效果 - 破阵时的环形扩展冲击波
    /// </summary>
    public class ShockwaveEffect : MonoBehaviour
    {
        [Header("冲击波参数")]
        [SerializeField] private float maxRadius = 15f;
        [SerializeField] private float expandSpeed = 20f;
        [SerializeField] private float duration = 1f;
        [SerializeField] private Material shockwaveMaterial;

        private float _timer;
        private bool _isPlaying;
        private MeshRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null)
                _renderer.enabled = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (evt.NewPhase == GamePhase.PoZhen)
                Play(Vector3.zero);
        }

        public void Play(Vector3 center)
        {
            transform.position = center;
            _timer = 0f;
            _isPlaying = true;
            if (_renderer != null)
                _renderer.enabled = true;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _timer += Time.deltaTime;
            float t = _timer / duration;

            if (t >= 1f)
            {
                _isPlaying = false;
                if (_renderer != null)
                    _renderer.enabled = false;
                return;
            }

            // 扩展半径
            float radius = t * maxRadius;
            transform.localScale = Vector3.one * radius;

            // 淡出
            if (shockwaveMaterial != null)
            {
                Color c = shockwaveMaterial.color;
                c.a = 1f - t;
                shockwaveMaterial.color = c;
            }
        }
    }

    /// <summary>
    /// VFX管理器 - 集中管理所有视觉效果的生命周期
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        [SerializeField] private ShockwaveEffect shockwavePrefab;
        [SerializeField] private FormationGlowEffect glowEffect;

        public static VFXManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public void TriggerShockwave(Vector3 position)
        {
            if (shockwavePrefab != null)
            {
                var wave = Instantiate(shockwavePrefab, position,
                    Quaternion.identity);
                wave.Play(position);
                Destroy(wave.gameObject, 2f);
            }
        }
    }
}
