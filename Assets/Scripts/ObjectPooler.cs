using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance;

    [System.Serializable]
    public class Pool
    {
        public string tag; // "BlackStone", "WhiteStone", "ForbiddenMark"
        public GameObject prefab;
        public int size; // 여유롭게 150개 정도
    }

    public List<Pool> pools;
    public Dictionary<string, Queue<GameObject>> poolDictionary;

    void Awake()
    {
        Instance = this;
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                obj.transform.SetParent(this.transform);
                objectPool.Enqueue(obj);
            }
            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    // 풀에서 꺼내기
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag)) return null;
        Queue<GameObject> queue = poolDictionary[tag];
        // ↓ 추가: 사용 가능한(비활성화된) 오브젝트 찾기, 없으면 새로 생성
        GameObject objectToSpawn = null;
        int count = queue.Count;
        for (int i = 0; i < count; i++)
        {
            GameObject obj = queue.Dequeue();
            if (!obj.activeInHierarchy)
            {
                objectToSpawn = obj;
                queue.Enqueue(obj);
                break;
            }
            queue.Enqueue(obj);
        }
        // 비활성 오브젝트가 없으면 새로 생성
        if (objectToSpawn == null)
        {
            Pool pool = pools.Find(p => p.tag == tag);
            if (pool != null)
            {
                objectToSpawn = Instantiate(pool.prefab);
                objectToSpawn.transform.SetParent(this.transform);
                queue.Enqueue(objectToSpawn);
                Debug.Log($"[ObjectPooler] {tag} 풀 부족 → 새 오브젝트 생성");
            }
        }

        // GameObject objectToSpawn = poolDictionary[tag].Dequeue();
        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        // poolDictionary[tag].Enqueue(objectToSpawn); // 다시 큐의 맨 뒤로 넣어서 재사용
        return objectToSpawn;
    }
}