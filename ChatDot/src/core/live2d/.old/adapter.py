import requests
#from emotion.adapter import EmotionAdapter
from live2d.emotion.adapter import EmotionAdapter
from global_managers.logger_manager import LoggerManager

class Live2DAdapter:
    def __init__(self, server_url: str = None, enable_emotion: bool = True):
        """
        初始化 Live2D 客户端
        :param server_url: Live2D 后端的服务器地址（可选）
        :param enable_emotion: 是否启用情感分析
        """
        self.server_url = server_url
        self.emotion_adapter = EmotionAdapter() if enable_emotion else None

    def set_server_url(self, server_url: str):
        """
        设置 Live2D 后端的服务器地址
        :param server_url: Live2D 后端的服务器地址
        """
        self.server_url = server_url

    def text_to_live2d(self, text: str):
        """
        接收文本，分析情感，并发送到 Live2D 后端
        :param text: 输入的文本
        """
        if not self.server_url:
            LoggerManager().get_logger().warning("警告: Live2D 后端 URL 未设置，无法处理请求")
            return

        try:
            # 调用情感分析
            if self.emotion_adapter:
                emotion_result = self.emotion_adapter.analyze_emotion(text)
                LoggerManager().get_logger().debug(f"情感分析结果: {emotion_result}")

                # 判断返回值类型并提取情感标签
                if isinstance(emotion_result, str):
                    emotion = emotion_result  # 如果是字符串，直接使用
                elif isinstance(emotion_result, list) and len(emotion_result) > 0:
                    emotion = emotion_result[0]['label']  # 如果是列表，提取第一个元素的标签
                else:
                    LoggerManager().get_logger().warning("无法识别情感，使用默认情感 neutral")
                    emotion = "neutral"
            else:
                LoggerManager().get_logger().warning("情感分析已禁用，使用默认情感 neutral")
                emotion = "neutral"

            # 构造请求数据
            payload = {"emotion": emotion}
            LoggerManager().get_logger().debug(f"发送情感数据到 Live2D 后端: {payload}")

            # 发送 POST 请求到 Live2D 后端
            response = requests.post(self.server_url, json=payload)

            # 检查响应状态
            if response.status_code == 200:
                LoggerManager().get_logger().debug("成功发送情感数据到 Live2D 后端")
            else:
                LoggerManager().get_logger().warning(f"发送失败，状态码: {response.status_code}, 响应: {response.text}")
        except Exception as e:
            LoggerManager().get_logger().warning(f"发送情感数据时发生错误: {e}")

# 示例用法
if __name__ == "__main__":
    # 初始化 Live2D 客户端，指定后端地址
    live2d_adapter = Live2DAdapter("http://localhost:9000")

    while True:
        # 输入文本
        text = input("请输入文本: ")
        if text.lower() == "exit":
            print("退出程序")
            break

        # 分析情感并发送到 Live2D 后端
        live2d_adapter.text_to_live2d(text)