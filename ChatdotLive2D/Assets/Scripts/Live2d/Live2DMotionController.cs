using UnityEngine;
using Live2D.Cubism.Framework.Motion;
using System.Collections.Generic;
using System.IO;

public class Live2DMotionController : MonoBehaviour
{
    public CubismMotionController motionController;
    public string animFolder = "Assets/Live2DModels/Epsilon/motions/";

    private (string motionName, float probability)[] idleMotions = new (string, float)[]
    {
        ("Epsilon_IdleMini", 0.7f),
        ("Epsilon_IdleBodyShake", 0.2f),
    };

    private Dictionary<string, AnimationClip> motionDict = new Dictionary<string, AnimationClip>();
    private bool isPlayingIdle = false;
    private bool isPlayingCommand = false;
    private AnimationClip currentCommandClip = null;
    private float commandStartTime = 0f;
    
    // 用于淡入淡出过渡的参数
    private bool pendingIdle = false;

    void Awake()
    {
        LoadAllAnimations();
        
        // 重要：注册动画结束回调
        if (motionController != null)
        {
            motionController.AnimationEndHandler += OnAnimationEnded;
        }
    }
    void Start()
    {
        PlayIdle(); // 确保一切初始化完成后播放待机动作
    }
    void OnDestroy()
    {
        // 清理回调
        if (motionController != null)
        {
            motionController.AnimationEndHandler -= OnAnimationEnded;
        }
    }

    private void LoadAllAnimations()
    {
        motionDict.Clear();
#if UNITY_EDITOR
        string[] animGuids = UnityEditor.AssetDatabase.FindAssets("t:AnimationClip", new[] { animFolder });
        foreach (var guid in animGuids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                motionDict[name] = clip;
            }
        }
#endif
        // 启动后播放待机动作
        PlayIdle();
    }

    // 从动画结束回调中调用
    private void OnAnimationEnded(int instanceId)
    {
        // 如果是命令动作播放结束，回到待机
        if (isPlayingCommand)
        {
            isPlayingCommand = false;
            PlayIdle();
        }
        // 如果是待机动作，继续播放待机
        else if (isPlayingIdle)
        {
            PlayIdle();
        }
    }

    public void PlayIdle()
    {
        string idleName = PickIdleMotion();
        if (!string.IsNullOrEmpty(idleName) && motionDict.TryGetValue(idleName, out var idleClip))
        {
            isPlayingIdle = true;
            isPlayingCommand = false;
            currentCommandClip = null;
            
            // idle 动作优先级低（如1）
            motionController.PlayAnimation(idleClip, layerIndex: 0, priority: 1, isLoop: false);
            Debug.Log("[Live2D] 播放待机动作: " + idleName);
        }
        else
        {
            Debug.LogWarning("[Live2D] 未找到可用的待机动作");
        }
    }

    private string PickIdleMotion()
    {
        float total = 0f;
        foreach (var entry in idleMotions)
            total += Mathf.Max(0, entry.probability);

        if (total <= 0f)
            return idleMotions[Random.Range(0, idleMotions.Length)].motionName;

        float r = Random.Range(0, total);
        float acc = 0f;
        foreach (var entry in idleMotions)
        {
            acc += Mathf.Max(0, entry.probability);
            if (r < acc)
                return entry.motionName;
        }
        return idleMotions[idleMotions.Length - 1].motionName;
    }

    public void PlayMotion(string motionName)
    {
        if (!motionDict.TryGetValue(motionName, out var clip))
        {
            Debug.LogWarning($"[Live2D] 未找到动作: {motionName}");
            return;
        }
        if (clip == null)
        {
            Debug.LogError($"[Live2D] 动作 {motionName} 的 AnimationClip 为 null！");
            return;
        }
        isPlayingIdle = false;
        isPlayingCommand = true;
        currentCommandClip = clip;
        commandStartTime = Time.time;
        
        // 指令动作优先级高（如3）
        motionController.StopAllAnimation();
        motionController.PlayAnimation(clip, layerIndex: 0, priority: 3, isLoop: false);
        Debug.Log($"[Live2D] 播放命令动作: {motionName}");
    }
    
    // 不再需要 Update 方法来检测动画结束
    // 现在完全依赖回调
}