using UnityEngine;
using System.Collections.Generic;

// 돌에 특정 색을 테두리처럼 입히는 임시 코드. 추후 셰이더로 구현 방식 변경 예정.
public class VisualOutline : MonoBehaviour
{
    private List<GameObject> outlineHulls = new List<GameObject>();
    public Material outlineMaterial;
    public float outlineThickness = 1.05f;

    public void EnableOutline(Color color)
    {
        if (outlineHulls.Count == 0)
        {
            // 몸통, 팔, 다리 등 모든 렌더러를 찾음
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            
            foreach (Renderer r in renderers)
            {
                // 테두리를 만들 빈 껍데기 오브젝트 생성
                GameObject hull = new GameObject("OutlineHull_" + r.gameObject.name);
                hull.transform.SetParent(r.transform, false);

                // MeshFilter 정보 복사
                MeshFilter originalMf = r.GetComponent<MeshFilter>();
                if (originalMf != null)
                {
                    MeshFilter hullMf = hull.AddComponent<MeshFilter>();
                    hullMf.sharedMesh = originalMf.sharedMesh;
                }
                // (※ SkinnedMeshRenderer의 경우 완벽하게 따라가지 않을 수 있습니다)

                MeshRenderer hullMr = hull.AddComponent<MeshRenderer>();
                hullMr.material = outlineMaterial;
                hullMr.material.color = color;

                hull.transform.localScale = Vector3.one * outlineThickness;
                outlineHulls.Add(hull);
            }
        }

        foreach (var hull in outlineHulls) hull.SetActive(true);
    }

    public void DisableOutline()
    {
        foreach (var hull in outlineHulls) hull.SetActive(false);
    }
}