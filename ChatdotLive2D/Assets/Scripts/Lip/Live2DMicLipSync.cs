using UnityEngine;
using Live2D.Cubism.Core;

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

    private AudioClip micClip;
    private CubismModel cubismModel;
    private int sampleLength = 256;
    private float[] samples;
    private float mouthOpen = 0f;
    private float mouthOpenSmoothed = 0f;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(micDevice) && Microphone.devices.Length > 0)
            micDevice = Microphone.devices[0];
    }

    void Start()
    {
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
        if (string.IsNullOrEmpty(micDevice))
            micDevice = Microphone.devices[0];

        micClip = Microphone.Start(micDevice, true, 1, 44100);
        samples = new float[sampleLength];
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