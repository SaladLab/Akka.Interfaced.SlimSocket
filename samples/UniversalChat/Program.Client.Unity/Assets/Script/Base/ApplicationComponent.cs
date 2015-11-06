using UnityEngine;

public class ApplicationComponent : MonoBehaviour
{
    public static ApplicationComponent Instance
    {
        get; private set;
    }

    public static bool TryInit()
    {
        if (Instance != null)
            return false;

        var go = new GameObject("_ApplicationComponent");
        Instance = go.AddComponent<ApplicationComponent>();
        DontDestroyOnLoad(go);
        return true;
    }
}
