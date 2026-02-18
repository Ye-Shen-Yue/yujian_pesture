using UnityEngine;

namespace YuJian.Core
{
    /// <summary>
    /// 游戏管理器 - 顶层编排器
    /// 初始化所有子系统，管理主更新循环
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("网络设置")]
        [SerializeField] private string wsHost = "localhost";
        [SerializeField] private int wsPort = 8765;

        [Header("子系统引用")]
        [SerializeField] private PhaseController phaseController;

        // 子系统引用（运行时获取）
        private Network.WebSocketClient _wsClient;
        private Network.GestureDataBuffer _dataBuffer;
        private InputBridge _inputBridge;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSubsystems();
        }

        private void InitializeSubsystems()
        {
            // 初始化网络
            _dataBuffer = new Network.GestureDataBuffer(capacity: 4);
            _wsClient = gameObject.AddComponent<Network.WebSocketClient>();
            _wsClient.Initialize(wsHost, wsPort, _dataBuffer);

            // 初始化输入桥接
            _inputBridge = gameObject.AddComponent<InputBridge>();
            _inputBridge.Initialize(_dataBuffer);

            // 初始化阶段控制器
            if (phaseController == null)
                phaseController = gameObject.AddComponent<PhaseController>();

            Debug.Log("[GameManager] 所有子系统初始化完成");
        }

        private void OnDestroy()
        {
            EventBus.Clear();
            if (Instance == this)
                Instance = null;
        }
    }
}
