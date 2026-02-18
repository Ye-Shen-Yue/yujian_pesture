using UnityEngine;
using YuJian.Core;
using YuJian.Network;

namespace YuJian.UI
{
    /// <summary>
    /// HUD控制器 - 阶段指示器和手势反馈
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private UnityEngine.UI.Text phaseText;
        [SerializeField] private UnityEngine.UI.Text gestureText;
        [SerializeField] private UnityEngine.UI.Text connectionText;

        private static readonly string[] PhaseNames = {
            "待机", "启阵", "布阵", "御剑", "破阵"
        };

        private static readonly string[] GestureNames = {
            "", "抓取", "推", "拉", "旋转", "捏合",
            "", "", "", "", "发射",
            "", "", "", "", "", "", "", "", "",
            "启阵", "破阵", "布阵", "定位"
        };

        private WebSocketClient _wsClient;
        private float _gestureDisplayTimer;

        private void Start()
        {
            _wsClient = FindObjectOfType<WebSocketClient>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Subscribe<GestureDetectedEvent>(OnGesture);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Unsubscribe<GestureDetectedEvent>(OnGesture);
        }

        private void Update()
        {
            // 连接状态
            if (connectionText != null && _wsClient != null)
            {
                connectionText.text = _wsClient.IsConnected ?
                    "已连接" : "未连接 - 等待Python引擎...";
                connectionText.color = _wsClient.IsConnected ?
                    Color.green : Color.red;
            }

            // 手势显示计时
            if (_gestureDisplayTimer > 0)
            {
                _gestureDisplayTimer -= Time.deltaTime;
                if (_gestureDisplayTimer <= 0 && gestureText != null)
                    gestureText.text = "";
            }
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (phaseText != null)
            {
                int idx = (int)evt.NewPhase;
                phaseText.text = idx < PhaseNames.Length ?
                    PhaseNames[idx] : evt.NewPhase.ToString();
            }
        }

        private void OnGesture(GestureDetectedEvent evt)
        {
            if (gestureText == null) return;

            int idx = (int)evt.Type;
            string name = idx < GestureNames.Length ?
                GestureNames[idx] : evt.Type.ToString();

            if (!string.IsNullOrEmpty(name))
            {
                gestureText.text = $"{name} ({evt.Confidence:P0})";
                _gestureDisplayTimer = 1.5f;
            }
        }
    }

    /// <summary>
    /// 教学引导覆盖层 - 90秒内引导用户学会基本操作
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        [Header("教学步骤")]
        [SerializeField] private UnityEngine.UI.Text instructionText;
        [SerializeField] private UnityEngine.UI.Image gestureImage;

        private int _currentStep;
        private bool _isActive;

        private static readonly string[] Steps = {
            "第一步：双手合十，然后缓缓拉开 → 启阵",
            "第二步：左手画圆选择阵法 → 布阵",
            "第三步：右手移动控制飞剑 → 御剑",
            "第四步：双掌快速合拢 → 破阵",
            "恭喜！你已掌握基本操作",
        };

        private void OnEnable()
        {
            EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
        }

        public void StartTutorial()
        {
            _isActive = true;
            _currentStep = 0;
            UpdateDisplay();
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (!_isActive) return;

            // 根据阶段推进教学步骤
            switch (evt.NewPhase)
            {
                case GamePhase.KaiZhen: _currentStep = 1; break;
                case GamePhase.BuZhen: _currentStep = 2; break;
                case GamePhase.YuJian: _currentStep = 3; break;
                case GamePhase.PoZhen: _currentStep = 4; break;
            }
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (instructionText != null && _currentStep < Steps.Length)
            {
                instructionText.text = Steps[_currentStep];
            }

            if (_currentStep >= Steps.Length - 1)
            {
                // 教学完成，延迟隐藏
                Invoke(nameof(HideTutorial), 3f);
            }
        }

        private void HideTutorial()
        {
            _isActive = false;
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 调试面板 - 显示延迟、FPS、手势状态
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text debugText;

        private float _fps;
        private float _fpsTimer;
        private int _frameCount;
        private InputBridge _input;

        private void Start()
        {
            _input = FindObjectOfType<InputBridge>();
        }

        private void Update()
        {
            // FPS计算
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0;
            }

            if (debugText == null) return;

            string text = $"FPS: {_fps:F1}\n";

            if (_input != null && _input.HasData)
            {
                var frame = _input.LatestFrame;
                text += $"Hands: {frame.Hands?.Count ?? 0}\n";
                text += $"Gestures: {frame.Gestures?.Count ?? 0}\n";
                text += $"Seq: {frame.Sequence}\n";
            }
            else
            {
                text += "No data\n";
            }

            debugText.text = text;
        }
    }
}
