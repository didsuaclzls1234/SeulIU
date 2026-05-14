//using UnityEngine;

//public class StoneOptimizer : MonoBehaviour
//{
//    private SkinnedMeshRenderer smr;
//    private MeshFilter mf;
//    private MeshRenderer mr;
//    private Animator anim;

//    private void Awake()
//    {
//        smr = GetComponentInChildren<SkinnedMeshRenderer>();
//        anim = GetComponent<Animator>();
//    }

//    // 1. 착수 모션이 끝나면 호출: 뼈대를 없애고 찰흙으로 굳혀버림 (렉 제로화)
//    public void BakeToStaticMesh()
//    {
//        if (smr == null) return;

//        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
//        if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();

//        Mesh bakedMesh = new Mesh();
//        smr.BakeMesh(bakedMesh);
//        mf.mesh = bakedMesh;
//        mr.materials = smr.materials;

//        // 🚨 [이게 핵심입니다!]
//        // 1. 기존 스킨드 메쉬의 그림자 권한을 완전히 뺏습니다. (Off)
//        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
//        smr.enabled = false;

//        // 2. 대신 새로 만든 일반 메쉬렌더러가 그림자를 담당하게 합니다.
//        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
//        mr.enabled = true;

//        if (anim != null) anim.enabled = false;
//    }

//    // 2. 승리 모션 때 호출: 찰흙을 부수고 다시 뼈대 캐릭터로 부활!
//    public void RestoreToSkinnedMesh()
//    {
//        // 다시 부활할 때 권한 복구
//        if (smr != null)
//        {
//            smr.enabled = true;
//            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
//        }

//        if (mr != null) mr.enabled = false;
//        if (anim != null) anim.enabled = true;
//    }
//}