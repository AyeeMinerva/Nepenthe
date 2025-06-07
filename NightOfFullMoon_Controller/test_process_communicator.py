import logging
from ProcessCommunicator import ProcessCommunicator

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

def main():
    comm_client = ProcessCommunicator.instance(is_server=False, host='127.0.0.1', port=5000)
    print("请输入要发送的内容，输入 'exit' 退出。")
    while True:
        thinking_message = input("请输入 <thinking> 内容: ")
        if thinking_message.lower() == 'exit':
            break
        comm_client.active = True
        topic = "Game.Description"
        comm_client.send(thinking_message, topic)
        logger.info(f"Thinking 消息已发送: {thinking_message} (主题: {topic})")
        comm_client.active = False

if __name__ == "__main__":
    main()