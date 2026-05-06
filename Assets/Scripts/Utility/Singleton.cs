using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool isApplicationQuit = false;

    public static T Instance
    {
        get
        {
            if (isApplicationQuit)
                return null;

            if (_instance == null)
            {
                T[] _finds = FindObjectsByType<T>(FindObjectsSortMode.None);

                if (_finds.Length > 0)
                {
                    _instance = _finds[0];
                    DontDestroyOnLoad(_instance.gameObject);
                }

                if (_finds.Length > 1)
                {
                    for (int i = 1; i < _finds.Length; i++)
                        Destroy(_finds[i].gameObject);
                }

                if (_instance == null)
                {
                    GameObject _createGameObject = new GameObject(typeof(T).Name);
                    DontDestroyOnLoad(_createGameObject);
                    _instance = _createGameObject.AddComponent<T>();
                }
            }
            return _instance;
        }
    }
    protected virtual void Awake()
    {
        // 씬이 시작되자마자, 이미 _instance가 할당되어 있고 그게 '나' 자신이 아니라면 Destory
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        // 내가 최초라면 _instance로 등록
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuit = true;
    }
}