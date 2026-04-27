// VisualOutline.cs (돌 프리팹에 ADD COMPONENT)
using UnityEngine;

public class VisualOutline : MonoBehaviour
{
    private MeshRenderer mainRenderer;
    private GameObject outlineHull; // 테두리를 위한 복제 메시

    [Header("Material Settings")]
    // 💡 테두리 전용 머티리얼을 여기에 할당하세요 (Unlit/Color 셰이더 권장)
    public Material outlineMaterial;
    public float outlineThickness = 1.05f; // 원본 크기의 1.05배

    private void Awake()
    {
        mainRenderer = GetComponent<MeshRenderer>();
    }

    // 테두리 활성화 (초록색, 빨간색 등 색상 전달 가능)
    public void EnableOutline(Color color)
    {
        if (outlineHull == null)
        {
            // 원본 돌의 MeshFilter와 MeshRenderer 정보를 복제
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // 1. 테두리용 빈 오브젝트 생성
            outlineHull = new GameObject("OutlineHull");
            outlineHull.transform.SetParent(this.transform, false); // 원본 돌의 자식으로 설정

            // 2. 메시 컴포넌트 추가 및 원본 메시 할당
            MeshFilter hullMf = outlineHull.AddComponent<MeshFilter>();
            hullMf.sharedMesh = mf.sharedMesh;

            // 3. 렌더러 추가 및 테두리 머티리얼 할당
            MeshRenderer hullMr = outlineHull.AddComponent<MeshRenderer>();
            hullMr.material = outlineMaterial; // 여기에 Inspector에서 미리 등록한 unlit 머티리얼이 들어옴

            // **  테두리 껍데기만 보이게 normal을 뒤집거나 inverted 셰이더를 써야 하는데, 
            // 여기선 SRP 원칙을 지키기 위해 unlit 머티리얼 색상만 변경하고 scaled Hull 방식으로 처리.

            // 4. 머티리얼 색상 변경 (전달받은 초록색 등)
            hullMr.material.color = color;
        }

        // 5. 원본 돌보다 약간 크게 스케일 조절 (테두리 효과)
        outlineHull.transform.localScale = Vector3.one * outlineThickness;
        outlineHull.SetActive(true);
    }

    public void DisableOutline()
    {
        if (outlineHull != null)
        {
            outlineHull.SetActive(false);
        }
    }
}