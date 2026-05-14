using UnityEngine;

public class BuildSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Debug.unityLogger.logEnabled = Debug.isDebugBuild;
    }
}