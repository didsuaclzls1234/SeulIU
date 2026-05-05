using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoneVisualController : MonoBehaviour
{
    private Renderer[] allRenderers;

    private readonly int OverlayColorID = Shader.PropertyToID("_OverlayColor");
    private readonly int OverlayBlendID = Shader.PropertyToID("_OverlayBlend");
    private readonly int GhostAlphaID = Shader.PropertyToID("_GhostAlpha");
    private readonly int IsConseratedID = Shader.PropertyToID("_IsConsecrated");
    private readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private readonly int MetallicID = Shader.PropertyToID("_Metallic");
    private readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");
    private readonly int OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");
    private readonly int OutlineGlowID = Shader.PropertyToID("_OutlineGlow");

    public bool IsVisible { get; private set; } = true;

    // 각 부품별 원래의 금속성/매끄러움 값을 기억하기 위한 저장소
    private class OriginalMatData
    {
        public float metallic;
        public float smoothness;
    }
    private Dictionary<Material, OriginalMatData> originalMatProps = new Dictionary<Material, OriginalMatData>();

    private void Awake()
    {
        allRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in allRenderers)
        {
            for (int i = 0; i < r.materials.Length; i++)
            {
                // 인스턴스로 복제
                r.materials[i] = new Material(r.materials[i]);
                Material mat = r.materials[i];

                // 원래 가지고 있던 텍스처와 반사광 수치를 딕셔너리에 기억해둠
                OriginalMatData data = new OriginalMatData();
                if (mat.HasProperty(MetallicID)) data.metallic = mat.GetFloat(MetallicID);
                if (mat.HasProperty(SmoothnessID)) data.smoothness = mat.GetFloat(SmoothnessID);
                originalMatProps[mat] = data;
            }
        }
    }

    public void SetVisibility(bool isVisible, bool isGhostMode, float ghostAlpha = 0.3f, float ghostMetallic = 0f, float ghostSmoothness = 0.1f)
    {
        IsVisible = isVisible;
        foreach (Renderer r in allRenderers)
        {
            r.enabled = isVisible;
            if (isVisible)
            {
                foreach (Material mat in r.materials)
                {
                    mat.SetFloat(GhostAlphaID, isGhostMode ? ghostAlpha : 1.0f);

                    if (mat.HasProperty(MetallicID))
                    {
                        if (isGhostMode)
                        {
                            // 투명화일 때는 기획자 세팅값(눈부심 방지용)으로 덮어씀
                            mat.SetFloat(MetallicID, ghostMetallic);
                            mat.SetFloat(SmoothnessID, ghostSmoothness);
                        }
                        else
                        {
                            // 투명화가 풀리면 기억해둔 원래 질감으로 완벽하게 원상복구
                            if (originalMatProps.TryGetValue(mat, out OriginalMatData data))
                            {
                                mat.SetFloat(MetallicID, data.metallic);
                                mat.SetFloat(SmoothnessID, data.smoothness);
                            }
                        }
                    }
                }
            }
        }
    }

    public void SetOverlay(Color color, float blendAmount)
    {
        foreach (Renderer r in allRenderers)
        {
            foreach (Material mat in r.materials)
            {
                mat.SetColor(OverlayColorID, color);
                mat.SetFloat(OverlayBlendID, blendAmount);
            }
        }
    }

    public void PlayBlinkEffect(Color blinkColor, float blendStrength)
    {
        StartCoroutine(BlinkRoutine(blinkColor, blendStrength));
    }

    private IEnumerator BlinkRoutine(Color color, float blendStrength)
    {
        for (int i = 0; i < 3; i++)
        {
            SetOverlay(color, blendStrength);
            yield return new WaitForSeconds(0.1f);
            SetOverlay(Color.black, 0f);
            yield return new WaitForSeconds(0.1f);
        }
    }

    public void SetConsecration(bool isActive, Color outlineColor, float thickness = 1.5f, float glow = 4.0f)
    {
        foreach (Renderer r in allRenderers)
        {
            foreach (Material mat in r.materials)
            {
                mat.SetFloat(IsConseratedID, isActive ? 1.0f : 0.0f);
                mat.SetColor(OutlineColorID, outlineColor);

                if (isActive)
                {
                    mat.SetFloat(OutlineThicknessID, thickness);
                    mat.SetFloat(OutlineGlowID, glow);
                }
            }
        }
    }
}