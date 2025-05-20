import pyaudio

import sounddevice as sd  # 需要 pip install sounddevice

p = pyaudio.PyAudio()
default_device = sd.default.device[1]  # [输入, 输出]

for i in range(p.get_device_count()):
    info = p.get_device_info_by_index(i)
    if info['maxOutputChannels'] > 0:
        mark = "<-- 默认" if i == default_device else ""
        print(i, info['name'], info['maxOutputChannels'], mark)