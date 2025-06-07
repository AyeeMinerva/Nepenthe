import pyaudio
import sounddevice as sd

p = pyaudio.PyAudio()
default_device = sd.default.device[1]

print(f"{'ID':<3} {'设备名称':<50} {'通道':<4} {'API':<15} {'状态'}")
print("-" * 80)

for i in range(p.get_device_count()):
    info = p.get_device_info_by_index(i)
    if info['maxOutputChannels'] > 0:
        mark = "默认" if i == default_device else ""
        # 获取主机API信息
        host_api = p.get_host_api_info_by_index(info['hostApi'])['name']
        device_name = info['name'][:45] + "..." if len(info['name']) > 45 else info['name']
        
        print(f"{i:<3} {device_name:<50} {info['maxOutputChannels']:<4} {host_api:<15} {mark}")

p.terminate()