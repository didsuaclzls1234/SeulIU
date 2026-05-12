using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SkillVFXManager : MonoBehaviour
{
    // 어디서든 쉽게 부를 수 있게 싱글톤 패턴 적용
    public static SkillVFXManager Instance;

    [Header("UI 이펙트 (Canvas 하위에 전체화면 Image 생성 후 연결)")]
    public Image screenOverlayImage; // 전체 화면 색상 덮기용 (신성화, 안티매직 점멸 등)
    public Image screenIconImage;    // 화면 중앙이나 귀퉁이에 띄울 이미지 (쇠사슬, 갈라짐, X표 등)

    [Header("카메라 연결")]
    public CameraSwitcher cameraSwitcher; // 현재 활성화된 카메라를 흔들기 위해 필요

    [Header("승리 연출용 파티클 (인스펙터 연결)")]
    public ParticleSystem confettiParticlePrefab; // 폭죽 꽃가루 파티클
    public GameObject fake2DSpotlightPrefab; // 🚨 방금 만든 가짜 조명 프리팹 연결!

    private GameObject[] victoryLights = new GameObject[3];
    // 🚨 LineRenderer 대신 우리가 만든 정석 클래스로 교체!
    private VolumetricBeam[] victoryBeams = new VolumetricBeam[3];
    private SpriteRenderer[] victoryCircles = new SpriteRenderer[3];

    [Header("스포트라이트 설정")]
    [Tooltip("원뿔 밑동의 반지름 (바닥 동그라미 크기에 맞춰 조절)")]
    public float beamBottomRadius = 1.6f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (screenOverlayImage) screenOverlayImage.gameObject.SetActive(false);
        if (screenIconImage) screenIconImage.gameObject.SetActive(false);
    }

    // =========================================================
    // 1. 카메라 지진 (칼날비, 칠죄종, 렌주룰 파괴 등에 사용)
    // =========================================================
    public void PlayCameraShake(float duration = 0.5f, float magnitude = 0.2f)
    {
        StartCoroutine(CameraShakeRoutine(duration, magnitude));
    }

    private IEnumerator CameraShakeRoutine(float duration, float magnitude)
    {
        // 현재 활성화된 카메라 찾기
        Camera activeCam = Camera.main;
        if (cameraSwitcher != null)
        {
            activeCam = cameraSwitcher.topCamera.depth > 0 ? cameraSwitcher.topCamera :
                        (cameraSwitcher.blackPlayerCamera.depth > 0 ? cameraSwitcher.blackPlayerCamera : cameraSwitcher.whitePlayerCamera);
        }

        if (activeCam == null) yield break;

        Vector3 originalPos = activeCam.transform.localPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            // 구체 안의 랜덤한 좌표로 미세하게 계속 이동시킴 (지진 효과)
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            activeCam.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        activeCam.transform.localPosition = originalPos; // 원상복구
    }

    // =========================================================
    // 2. 전체 화면 점멸 / 색상 덮기 (신성화 팟!, 안티매직 하늘색 번쩍 등)
    // =========================================================
    public void PlayScreenFlash(Color flashColor, float duration = 1.0f)
    {
        if (screenOverlayImage == null) return;
        StartCoroutine(ScreenFlashRoutine(flashColor, duration));
    }

    private IEnumerator ScreenFlashRoutine(Color color, float duration)
    {
        screenOverlayImage.color = color;
        screenOverlayImage.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 서서히 투명해짐 (Alpha값 1 -> 0)
            float alpha = Mathf.Lerp(color.a, 0f, elapsed / duration);
            screenOverlayImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        screenOverlayImage.gameObject.SetActive(false);
    }

    // =========================================================
    // 3. 화면에 특정 이미지 띄웠다 사라지게 하기 (칼날비 화면 갈라짐, 룰파괴 망치 등)
    // =========================================================
    public void PlayScreenIcon(Sprite icon, float duration = 1.0f, float scaleMultiplier = 2f)
    {
        if (screenIconImage == null || icon == null) return;
        StartCoroutine(ScreenIconRoutine(icon, duration, scaleMultiplier));
    }

    private IEnumerator ScreenIconRoutine(Sprite icon, float duration, float scale)
    {
        screenIconImage.sprite = icon;
        screenIconImage.color = Color.white;
        screenIconImage.transform.localScale = Vector3.one * scale;
        screenIconImage.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 크기는 줄어들고, 투명도는 옅어짐 (팍! 꽂히고 스르륵 사라지는 느낌)
            screenIconImage.transform.localScale = Vector3.Lerp(Vector3.one * scale, Vector3.one, t);
            screenIconImage.color = new Color(1, 1, 1, 1f - t);
            yield return null;
        }

        screenIconImage.gameObject.SetActive(false);
    }

    // =========================================================
    // 4. 오브젝트 직접 조작: 제거 스킬 (지지직 떨리며 축소)
    // =========================================================
    public void PlayGlitchDeath(GameObject targetObj)
    {
        if (targetObj != null) StartCoroutine(GlitchDeathRoutine(targetObj));
    }

    private IEnumerator GlitchDeathRoutine(GameObject obj)
    {
        Vector3 originalScale = obj.transform.localScale;
        Vector3 originalPos = obj.transform.position;
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 좌표를 마구 흔듦 (글리치 효과)
            obj.transform.position = originalPos + (Random.insideUnitSphere * 0.15f);

            // 크기는 0으로 쫙 찌그러짐
            obj.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            yield return null;
        }

        obj.SetActive(false);
        // 다음 판이나 오브젝트 풀링을 위해 원래 상태로 돌려놓음 (매우 중요!)
        obj.transform.position = originalPos;
        obj.transform.localScale = originalScale;
    }

    // =========================================================
    // 5. 무대 스포트라이트 & 폭죽 (2D 스타일 개조판)
    // =========================================================
    public void PlayVictoryStageEffect(Vector3 targetCenter, float duration)
    {
        // 1. 폭죽 팡! (프리팹 위치에서 고정 생성)
        if (confettiParticlePrefab != null)
        {
            ParticleSystem confetti = Instantiate(confettiParticlePrefab);
            confetti.Play();
            Destroy(confetti.gameObject, duration + 2f);
        }

        // 2. 가짜 2D 조명 3개 움직이기
        if (fake2DSpotlightPrefab != null)
        {
            StartCoroutine(SearchingFakeSpotlightRoutine(targetCenter, duration));
        }
    }

    private IEnumerator SearchingFakeSpotlightRoutine(Vector3 center, float duration)
    {
        float height = 7f;
        float radius = 10f; // 양쪽으로 쫙 벌린 넓은 간격

        Vector3[] skyPositions = new Vector3[3];
        Vector3[] randomTargets = new Vector3[3];

        for (int i = 0; i < 3; i++)
        {
            if (victoryLights[i] == null)
            {
                victoryLights[i] = Instantiate(fake2DSpotlightPrefab);
                // 🚨 LineRenderer 대신 VolumetricBeam 컴포넌트를 가져옴
                victoryBeams[i] = victoryLights[i].GetComponent<VolumetricBeam>();
                victoryCircles[i] = victoryLights[i].GetComponentInChildren<SpriteRenderer>();
            }

            victoryLights[i].SetActive(true);

            float angle = i * 120f;
            skyPositions[i] = center + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, height, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
            randomTargets[i] = center + new Vector3(Random.Range(-8f, 8f), 0.01f, Random.Range(-8f, 8f));
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveT = t * t * (3f - 2f * t);

            for (int i = 0; i < 3; i++)
            {
                Vector3 currentFloorTarget = Vector3.Lerp(randomTargets[i], center, curveT);
                Vector3 perfectGroundPos = new Vector3(currentFloorTarget.x, 0.01f, currentFloorTarget.z);

                if (victoryLights[i] != null)
                    victoryLights[i].transform.position = new Vector3(currentFloorTarget.x, 0.01f, currentFloorTarget.z);

                // 뽀샤시하고 투명한 2D 느낌의 색상 조합
                Color startColor = new Color(1f, 0.8f, 1f, 0.05f);
                Color endColor = new Color(1f, 0.8f, 1f, 0.05f);
                Color currentColor = Color.Lerp(startColor, endColor, curveT);

                if (victoryCircles[i] != null) victoryCircles[i].color = currentColor;

                // 직접 깎은 3D 원뿔 메쉬 업데이트!
                if (victoryBeams[i] != null)
                {
                    Color topColor = new Color(currentColor.r, currentColor.g, currentColor.b, 0f); // 위는 투명하게
                    Color bottomColor = currentColor; // 아래는 동그라미랑 똑같은 색으로

                    // 시작점, 끝점, 위쪽 두께(0.1f), 아래쪽 두께(beamBottomRadius)
                    victoryBeams[i].UpdateBeam(skyPositions[i], perfectGroundPos, 0.1f, beamBottomRadius, topColor, bottomColor);
                }
            }
            yield return null;
        }
    }

    // 다음 판 시작 시 불 끄는 함수
    public void ClearVictoryStageEffect()
    {
        for (int i = 0; i < 3; i++)
        {
            if (victoryLights[i] != null) victoryLights[i].SetActive(false);
        }
    }

}