using UnityEngine;

public class RayTest : MonoBehaviour

{
   private void Update()
    {
        // 자기 자신 바로 위에서 아래로 레이캐스트
        Vector3 origin = transform.position + Vector3.up * 10f;
        
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f))
            Debug.Log($"히트: {hit.collider.gameObject.name}");
        else
            Debug.Log("미스");
    }
}