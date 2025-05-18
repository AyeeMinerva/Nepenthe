using UnityEngine;
using Live2D.Cubism.Framework.Motion;

public class TestMotionPlayer : MonoBehaviour
{
    public CubismMotionController motionController;
    public AnimationClip testClip;

    void OnGUI()
    {
        if (GUILayout.Button("播放测试动作"))
        {
            if (motionController != null && testClip != null)
            {
                Debug.Log($"[Test] 尝试播放动画: {testClip.name}, 类型: {testClip.GetType()}");
                motionController.PlayAnimation(testClip, isLoop: false);
            }
            else
            {
                Debug.LogWarning("[Test] motionController 或 testClip 未赋值！");
            }
        }
    }
}