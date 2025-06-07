using UnityEngine;
using Live2D.Cubism.Core;
using System.Linq;

public class Live2DMicLipSync : MonoBehaviour
{
    [Header("Live2D模型")]
    public GameObject live2DModel;
    [Header("麦克风设备")]
    public string micDevice; // Inspector会自动提供下拉
    [Header("音量到口型的放大系数")]
    public float volumeScale = 10f;
    [Header("口型参数ID")]
    public string mouthParamId = "PARAM_MOUTH_OPEN_Y";
    [Header("口型平滑系数(0~1, 越大越平滑)")]
    [Range(0f, 1f)]
    public float smoothFactor = 0.7f;
    [Header("自动检测CABLE Output")]
    public bool autoDetectCableOutput = true;
    [Header("CABLE设备关键词")]
    public string[] cableKeywords = { "CABLE Output", "VB-Audio Virtual Cable", "CABLE" };

    private AudioClip micClip;
    private CubismModel cubismModel;
    private int sampleLength = 256;
    private float[] samples;
    private float mouthOpen = 0f;
    private float mouthOpenSmoothed = 0f;

    void OnValidate()
    {
        if (autoDetectCableOutput)
        {
            DetectAndSetCableOutput();
        }
        else if (string.IsNullOrEmpty(micDevice) && Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
        }
    }

    void Start()
    {
        // 动态检测CABLE Output
        if (autoDetectCableOutput)
        {
            DetectAndSetCableOutput();
        }

        if (live2DModel == null)
        {
            Debug.LogError("请在Inspector中指定Live2D模型！");
            enabled = false;
            return;
        }
        cubismModel = live2DModel.GetComponent<CubismModel>();
        if (cubismModel == null)
        {
            Debug.LogError("Live2D模型上缺少CubismModel组件！");
            enabled = false;
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("未检测到任何麦克风设备！");
            enabled = false;
            return;
        }

        // 验证设备是否仍然可用
        if (!IsDeviceAvailable(micDevice))
        {
            Debug.LogWarning($"指定的麦克风设备 '{micDevice}' 不可用，重新检测...");
            DetectAndSetCableOutput();
        }

        if (string.IsNullOrEmpty(micDevice))
        {
            micDevice = Microphone.devices[0];
        }

        Debug.Log($"使用麦克风设备: {micDevice}");
        micClip = Microphone.Start(micDevice, true, 1, 44100);
        samples = new float[sampleLength];
    }

    /// <summary>
    /// 检测并设置CABLE Output设备
    /// </summary>
    private void DetectAndSetCableOutput()
    {
        LogAllMicrophoneDevices();

        string detectedDevice = FindCableOutputDevice();
        if (!string.IsNullOrEmpty(detectedDevice))
        {
            micDevice = detectedDevice;
            Debug.Log($"自动检测到CABLE Output设备: {micDevice}");
        }
        else
        {
            Debug.LogWarning("未找到CABLE Output设备，使用默认麦克风");
            if (Microphone.devices.Length > 0)
            {
                micDevice = Microphone.devices[0];
            }
        }
    }

    /// <summary>
    /// 查找CABLE Output设备
    /// </summary>
    private string FindCableOutputDevice()
    {
        foreach (string device in Microphone.devices)
        {
            foreach (string keyword in cableKeywords)
            {
                if (device.ToLower().Contains(keyword.ToLower()))
                {
                    return device;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 检查设备是否可用
    /// </summary>
    private bool IsDeviceAvailable(string deviceName)
    {
        return Microphone.devices.Contains(deviceName);
    }

    /// <summary>
    /// 记录所有可用的麦克风设备
    /// </summary>
    private void LogAllMicrophoneDevices()
    {
        Debug.Log("=== 可用麦克风设备列表 ===");
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            Debug.Log($"[{i}] {Microphone.devices[i]}");
        }
        Debug.Log("========================");
    }

    /// <summary>
    /// 运行时切换麦克风设备
    /// </summary>
    public void SwitchMicrophoneDevice(string newDevice)
    {
        if (!IsDeviceAvailable(newDevice))
        {
            Debug.LogError($"设备 '{newDevice}' 不可用");
            return;
        }

        // 停止当前录音
        if (!string.IsNullOrEmpty(micDevice))
        {
            Microphone.End(micDevice);
        }

        // 切换到新设备
        micDevice = newDevice;
        micClip = Microphone.Start(micDevice, true, 1, 44100);
        Debug.Log($"已切换到麦克风设备: {micDevice}");
    }

    /// <summary>
    /// 刷新并重新检测CABLE设备
    /// </summary>
    [ContextMenu("刷新并检测CABLE设备")]
    public void RefreshAndDetectCable()
    {
        DetectAndSetCableOutput();
        
        if (Application.isPlaying && !string.IsNullOrEmpty(micDevice))
        {
            // 重新启动录音
            if (micClip != null)
            {
                Microphone.End(micDevice);
            }
            micClip = Microphone.Start(micDevice, true, 1, 44100);
        }
    }

    void Update()
    {
        if (micClip == null || cubismModel == null) return;

        int micPos = Microphone.GetPosition(micDevice) - sampleLength;
        if (micPos < 0) return;

        micClip.GetData(samples, micPos);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        float rms = Mathf.Sqrt(sum / samples.Length);

        mouthOpen = Mathf.Clamp01(rms * volumeScale);

        // 平滑处理
        mouthOpenSmoothed = Mathf.Lerp(mouthOpenSmoothed, mouthOpen, 1f - smoothFactor);
    }

    void LateUpdate()
    {
        if (cubismModel == null) return;

        var mouthParam = cubismModel.Parameters.FindById(mouthParamId);
        if (mouthParam != null)
        {
            mouthParam.Value = mouthOpenSmoothed;
        }
    }

    void OnDestroy()
    {
        if (!string.IsNullOrEmpty(micDevice))
            Microphone.End(micDevice);
    }
}