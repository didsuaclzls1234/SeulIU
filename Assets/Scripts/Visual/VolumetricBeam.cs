using UnityEngine;

// 이 스크립트를 넣으면 메쉬 필터와 렌더러가 자동으로 찰떡같이 붙습니다.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VolumetricBeam : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Color[] colors;
    private int segments = 16; // 원뿔을 얼마나 둥글게 깎을지 (16각기둥)

    void Awake()
    {
        mesh = new Mesh();
        mesh.MarkDynamic(); // 실시간으로 변하는 메쉬라고 유니티에 알려줌 (최적화)
        GetComponent<MeshFilter>().mesh = mesh;
    }

    // 매 프레임 조명이 움직일 때마다 SkillVFXManager가 이 함수를 호출할 겁니다.
    public void UpdateBeam(Vector3 start, Vector3 end, float startRadius, float endRadius, Color topColor, Color bottomColor)
    {
        Vector3 localStart = transform.InverseTransformPoint(start);
        Vector3 localEnd = transform.InverseTransformPoint(end);

        int numVertices = segments * 2;
        int numTriangles = segments * 6;

        if (vertices == null || vertices.Length != numVertices)
        {
            vertices = new Vector3[numVertices];
            triangles = new int[numTriangles];
            colors = new Color[numVertices];
        }

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            // 🚨 핵심 수정: 대각선 회전 다 갖다 버리고, 무조건 '바닥과 완벽하게 평행한 원(XZ평면)'으로 고정!
            Vector3 flatCirclePos = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            // 위쪽 꼭짓점 (하늘)
            vertices[i] = localStart + flatCirclePos * startRadius;
            colors[i] = topColor;

            // 아래쪽 꼭짓점 (바닥) - 이제 FloorCircle(반지름 2f)과 오차 1mm도 없이 100% 일치합니다.
            vertices[i + segments] = localEnd + flatCirclePos * endRadius;
            colors[i + segments] = bottomColor;
        }

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            triangles[t++] = i;
            triangles[t++] = i + segments;
            triangles[t++] = next;

            triangles[t++] = next;
            triangles[t++] = i + segments;
            triangles[t++] = next + segments;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }
}