"""WebSocket服务端 - 异步广播手势数据到Unity客户端"""

import asyncio
import logging
import websockets

logger = logging.getLogger(__name__)


class WebSocketServer:
    """异步WebSocket服务端

    支持多客户端连接，广播模式发送数据。
    运行在独立线程中，通过send_queue接收主线程数据。
    """

    def __init__(self, host: str = "localhost", port: int = 8765):
        self.host = host
        self.port = port
        self._clients: set = set()
        self._send_queue: asyncio.Queue = None
        self._server = None
        self._loop: asyncio.AbstractEventLoop = None
        self._thread = None
        self._running = False

    async def _handler(self, websocket):
        """处理新客户端连接"""
        self._clients.add(websocket)
        client_addr = websocket.remote_address
        logger.info(f"Unity客户端已连接: {client_addr}")
        try:
            async for message in websocket:
                # 接收Unity端的消息（如配置更新、阶段反馈）
                logger.debug(f"收到客户端消息: {len(message)} bytes")
        except websockets.ConnectionClosed:
            pass
        finally:
            self._clients.discard(websocket)
            logger.info(f"Unity客户端已断开: {client_addr}")

    async def _broadcast_loop(self):
        """从队列取数据并广播给所有客户端"""
        while self._running:
            try:
                data = await asyncio.wait_for(
                    self._send_queue.get(), timeout=0.1
                )
                if self._clients:
                    # 并发发送给所有客户端
                    await asyncio.gather(
                        *[self._safe_send(client, data)
                          for client in self._clients.copy()],
                        return_exceptions=True,
                    )
            except asyncio.TimeoutError:
                continue
            except Exception as e:
                logger.error(f"广播错误: {e}")

    async def _safe_send(self, client, data):
        """安全发送，连接断开时自动移除客户端"""
        try:
            await client.send(data)
        except websockets.ConnectionClosed:
            self._clients.discard(client)

    async def _run(self):
        """启动服务端主循环"""
        self._send_queue = asyncio.Queue(maxsize=4)
        self._running = True

        self._server = await websockets.serve(
            self._handler, self.host, self.port
        )
        logger.info(f"WebSocket服务端启动: ws://{self.host}:{self.port}")

        # 同时运行连接处理和广播循环
        broadcast_task = asyncio.create_task(self._broadcast_loop())
        try:
            await self._server.wait_closed()
        finally:
            self._running = False
            broadcast_task.cancel()

    def start_in_thread(self):
        """在独立线程中启动服务端"""
        import threading

        def _thread_target():
            self._loop = asyncio.new_event_loop()
            asyncio.set_event_loop(self._loop)
            self._loop.run_until_complete(self._run())

        self._thread = threading.Thread(target=_thread_target, daemon=True)
        self._thread.start()
        logger.info("WebSocket服务端线程已启动")

    def send(self, data):
        """从主线程发送数据（线程安全）

        接受str(JSON)或bytes，如果队列满则丢弃旧数据（保证实时性）
        """
        if self._loop is None or self._send_queue is None:
            return

        async def _enqueue():
            if self._send_queue.full():
                try:
                    self._send_queue.get_nowait()  # 丢弃最旧的帧
                except asyncio.QueueEmpty:
                    pass
            await self._send_queue.put(data)

        asyncio.run_coroutine_threadsafe(_enqueue(), self._loop)

    @property
    def client_count(self) -> int:
        return len(self._clients)

    def stop(self):
        """停止服务端"""
        self._running = False
        if self._server:
            self._server.close()
        if self._loop:
            self._loop.call_soon_threadsafe(self._loop.stop)
        logger.info("WebSocket服务端已停止")
