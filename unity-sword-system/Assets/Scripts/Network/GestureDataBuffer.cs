using YuJian.Core;

namespace YuJian.Network
{
    /// <summary>
    /// 线程安全的环形缓冲区
    /// 存储最近N帧手势数据，Unity主线程每帧读取最新帧
    /// </summary>
    public class GestureDataBuffer
    {
        private readonly GestureFrame[] _buffer;
        private readonly int _capacity;
        private int _writeIndex;
        private readonly object _lock = new object();
        private bool _hasData;
        private int _lastWrittenSeq;   // 最后写入的序列号
        private int _lastReadSeq;      // 最后读取的序列号

        public GestureDataBuffer(int capacity = 4)
        {
            _capacity = capacity;
            _buffer = new GestureFrame[capacity];
            _writeIndex = 0;
            _hasData = false;
            _lastWrittenSeq = 0;
            _lastReadSeq = 0;
        }

        /// <summary>写入一帧数据（网络线程调用）</summary>
        public void Write(GestureFrame frame)
        {
            lock (_lock)
            {
                _buffer[_writeIndex] = frame;
                _writeIndex = (_writeIndex + 1) % _capacity;
                _lastWrittenSeq = frame.Sequence;
                _hasData = true;
            }
        }

        /// <summary>读取最新帧，仅当有新数据时返回非null（Unity主线程调用）</summary>
        public GestureFrame GetLatest()
        {
            lock (_lock)
            {
                if (!_hasData) return null;
                // 只有新帧才返回，避免重复处理同一帧
                if (_lastWrittenSeq == _lastReadSeq) return null;
                int latestIndex = (_writeIndex - 1 + _capacity) % _capacity;
                _lastReadSeq = _lastWrittenSeq;
                return _buffer[latestIndex];
            }
        }

        /// <summary>是否有数据</summary>
        public bool HasData
        {
            get { lock (_lock) { return _hasData; } }
        }

        /// <summary>清空缓冲区</summary>
        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < _capacity; i++)
                    _buffer[i] = null;
                _writeIndex = 0;
                _lastWrittenSeq = 0;
                _lastReadSeq = 0;
                _hasData = false;
            }
        }
    }
}
