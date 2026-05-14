using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SkillVFXManager : Singleton<SkillVFXManager>
{
    [Header("UI 이펙트 (Canvas 하위에 전체화면 Image 생성 후 연결)")]
    public Image screenOverlayImage; // 전체 화면 색상 덮기용 (신성화, 안티매직 점멸 등)
    public Image screenIconImage;    // 화면 중앙이나 귀퉁이에 띄울 이미지 (쇠사슬, 갈라짐, X표 등)

    // 🚨 [추가] 유지형 오버레이 레이어 4개로 분리!
    public Image antiMagicOverlayImage;
    public Image sevenSinsOverlayImage;
    public Image invisibilityOverlayImage;
    public Image consecrationOverlayImage; // 🚨 새로 만든 레이어 연결

    [Header("카메라 연결")]
    public CameraSwitcher cameraSwitcher; // 현재 활성화된 카메라를 흔들기 위해 필요

    [Header("승리 연출용 파티클 (인스펙터 연결)")]
    public ParticleSystem confettiParticlePrefab; // 폭죽 꽃가루 파티클
    public GameObject fake2DSpotlightPrefab; // 가짜 조명 프리팹 연결!

    private GameObject[] victoryLights = new GameObject[3];
    private VolumetricBeam[] victoryBeams = new VolumetricBeam[3];
    private SpriteRenderer[] victoryCircles = new SpriteRenderer[3];

    [Header("스포트라이트 설정")]
    [Tooltip("원뿔 밑동의 반지름 (바닥 동그라미 크기에 맞춰 조절)")]
    public float beamBottomRadius = 1.6f;

    // 🚨 [추가] 생성된 꽃가루를 기억할 변수
    private ParticleSystem activeConfetti;

    protected override void Awake()
    {
        base.Awake();
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
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            activeCam.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        activeCam.transform.localPosition = originalPos;
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
            float alpha = Mathf.Lerp(color.a, 0f, elapsed / duration);
            screenOverlayImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        screenOverlayImage.gameObject.SetActive(false);
    }

    // =========================================================
    // 3. 화면에 특정 이미지 띄웠다 사라지게 하기 
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

            screenIconImage.transform.localScale = Vector3.Lerp(Vector3.one * scale, Vector3.one, t);
            screenIconImage.color = new Color(1, 1, 1, 1f - t);
            yield return null;
        }

        screenIconImage.gameObject.SetActive(false);
    }

    // =========================================================
    // 🚨 [복구됨!] 4. 제거 (지지직 떨리며 축소)
    // =========================================================
    public void PlayGlitchDeath(GameObject obj, StoneColor trueColor)
    {
        if (obj != null) StartCoroutine(GlitchDeathRoutine(obj, trueColor));
    }

    private IEnumerator GlitchDeathRoutine(GameObject obj, StoneColor trueColor)
    {
        // 1. 투명화/신성화 등 모든 가짜 상태 해제 및 본모습 강제 적용
        StoneVisualController svc = obj.GetComponent<StoneVisualController>();
        if (svc != null)
        {
            svc.SetVisibility(true, false); // 투명화 강제 해제 (완전 불투명)
            svc.SetConsecration(false, Color.black); // 신성화 테두리 제거
        }

        // 2. 파괴 직전 붉게 3번 짧게 점멸! (경고 느낌)
        for (int i = 0; i < 3; i++)
        {
            if (svc != null) svc.SetOverlay(Color.red, 0.8f);
            yield return new WaitForSeconds(0.1f);
            if (svc != null) svc.SetOverlay(Color.black, 0f);
            yield return new WaitForSeconds(0.1f);
        }

        // 3. 지지직거리며 파괴 연출
        Vector3 originalScale = obj.transform.localScale;
        Vector3 originalPos = obj.transform.position;
        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 미친듯한 지진
            obj.transform.position = originalPos + (Random.insideUnitSphere * 0.5f);

            // 가로세로 납작하게 글리치
            float randomStretch = Random.Range(0.2f, 3.0f);
            obj.transform.localScale = new Vector3(
                originalScale.x * randomStretch * (1 - t),
                originalScale.y * (1f / randomStretch) * (1 - t),
                originalScale.z * randomStretch * (1 - t)
            );
            yield return null;
        }

        obj.SetActive(false); // 🚨 완전히 폭발 후 비활성화 (풀 반환)
        obj.transform.position = originalPos;
        obj.transform.localScale = originalScale;
    }

    // =========================================================
    // 🚨 [복구됨!] 5. 돌 이동 (텔레포트 슉~ 쾅!)
    // =========================================================
    public void PlayTeleportVFX(GameObject stone, Vector3 startPos, Vector3 endPos)
    {
        if (stone != null) StartCoroutine(TeleportRoutine(stone, startPos, endPos));
    }

    private IEnumerator TeleportRoutine(GameObject stone, Vector3 startPos, Vector3 endPos)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 origScale = stone.transform.localScale;

        // 이탈: 하늘로 슉!
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            stone.transform.localScale = new Vector3(origScale.x * (1 - t), origScale.y * (1 + t), origScale.z * (1 - t));
            stone.transform.position = startPos + Vector3.up * (t * 2f);
            yield return null;
        }

        stone.transform.localScale = Vector3.zero;
        yield return new WaitForSeconds(0.1f);

        // 도착: 바닥으로 쾅!
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            stone.transform.localScale = new Vector3(origScale.x * t, origScale.y * (2 - t), origScale.z * t);
            stone.transform.position = endPos + Vector3.up * (2f - (t * 2f));
            yield return null;
        }

        // 찌부러짐
        stone.transform.localScale = new Vector3(origScale.x * 1.2f, origScale.y * 0.8f, origScale.z * 1.2f);
        stone.transform.position = endPos;
        yield return new WaitForSeconds(0.05f);
        stone.transform.localScale = origScale;
    }

    // =========================================================
    // 6. 무대 스포트라이트 & 폭죽
    // =========================================================
    public void PlayVictoryStageEffect(Vector3 targetCenter, float duration)
    {
        if (confettiParticlePrefab != null)
        {
            // 🚨 [수정] Destroy 타이머를 없애고 변수에 저장만 해둡니다!
            if (activeConfetti != null) Destroy(activeConfetti.gameObject); // 혹시 남아있던 거 치우기
            activeConfetti = Instantiate(confettiParticlePrefab);
            activeConfetti.Play();
        }

        if (fake2DSpotlightPrefab != null)
        {
            StartCoroutine(SearchingFakeSpotlightRoutine(targetCenter, duration));
        }
    }

    private IEnumerator SearchingFakeSpotlightRoutine(Vector3 center, float duration)
    {
        float height = 7f;
        float radius = 10f;

        Vector3[] skyPositions = new Vector3[3];
        Vector3[] randomTargets = new Vector3[3];

        for (int i = 0; i < 3; i++)
        {
            if (victoryLights[i] == null)
            {
                victoryLights[i] = Instantiate(fake2DSpotlightPrefab);
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

            //float curveT = t * t * (3f - 2f * t);

            // 🚨 [수정] 집중 속도 향상: 기존 curveT보다 더 빠르게 중심에 도달하도록 변경
            // t * t * t (Cubic Ease-In) 또는 1 - (1-t)^3 (Cubic Ease-Out) 느낌을 섞어 
            // 초반 텐션을 훨씬 높였습니다.
            float curveT = 1f - Mathf.Pow(1f - t, 3f);

            for (int i = 0; i < 3; i++)
            {
                Vector3 currentFloorTarget = Vector3.Lerp(randomTargets[i], center, curveT);
                Vector3 perfectGroundPos = new Vector3(currentFloorTarget.x, 0.01f, currentFloorTarget.z);

                if (victoryLights[i] != null)
                    victoryLights[i].transform.position = new Vector3(currentFloorTarget.x, 0.01f, currentFloorTarget.z);

                Color startColor = new Color(1f, 0.8f, 1f, 0.05f);
                Color endColor = new Color(1f, 0.8f, 1f, 0.05f);
                Color currentColor = Color.Lerp(startColor, endColor, curveT);

                if (victoryCircles[i] != null) victoryCircles[i].color = currentColor;

                if (victoryBeams[i] != null)
                {
                    Color topColor = new Color(currentColor.r, currentColor.g, currentColor.b, 0f);
                    Color bottomColor = currentColor;
                    victoryBeams[i].UpdateBeam(skyPositions[i], perfectGroundPos, 0.1f, beamBottomRadius, topColor, bottomColor);
                }
            }
            yield return null;
        }
    }

    public void ClearVictoryStageEffect()
    {
        for (int i = 0; i < 3; i++)
        {
            if (victoryLights[i] != null) victoryLights[i].SetActive(false);
        }

        // 🚨 [추가] 다음 게임을 위해 바닥에 떨어진 꽃가루 싹 치우기!
        if (activeConfetti != null)
        {
            Destroy(activeConfetti.gameObject);
            activeConfetti = null;
        }

    }

    // =========================================================
    // 7. 카메라 FOV 펀치 
    // =========================================================
    public void PlayFOVPunch(float duration = 0.2f, float zoomAmount = 10f)
    {
        StartCoroutine(FOVPunchRoutine(duration, zoomAmount));
    }

    private IEnumerator FOVPunchRoutine(float duration, float zoomAmount)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        float origFOV = cam.fieldOfView;
        float targetFOV = origFOV - zoomAmount;
        float elapsed = 0f;

        while (elapsed < duration * 0.2f)
        {
            elapsed += Time.deltaTime;
            cam.fieldOfView = Mathf.Lerp(origFOV, targetFOV, elapsed / (duration * 0.2f));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration * 0.8f)
        {
            elapsed += Time.deltaTime;
            cam.fieldOfView = Mathf.Lerp(targetFOV, origFOV, elapsed / (duration * 0.8f));
            yield return null;
        }
        cam.fieldOfView = origFOV;
    }

    // =========================================================
    // 8. 기본 도형 쉴드
    // =========================================================
    public void PlayPrimitiveShield(Vector3 pos, Color color)
    {
        StartCoroutine(PrimitiveShieldRoutine(pos, color));
    }

    private IEnumerator PrimitiveShieldRoutine(Vector3 pos, Color color)
    {
        GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(shield.GetComponent<Collider>());
        shield.transform.position = pos;

        MeshRenderer renderer = shield.GetComponent<MeshRenderer>();
        if (fake2DSpotlightPrefab != null)
            renderer.sharedMaterial = fake2DSpotlightPrefab.GetComponent<MeshRenderer>().sharedMaterial;

        Vector3 startScale = new Vector3(0.1f, 0.05f, 0.1f);
        Vector3 endScale = new Vector3(3f, 0.05f, 3f);

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            shield.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        Destroy(shield, 0.1f);
    }

    // =========================================================
    // 9. 돌 자체 색상 깜빡임
    // =========================================================
    public void PlayStoneBlink(GameObject stone)
    {
        StartCoroutine(StoneBlinkRoutine(stone));
    }

    private IEnumerator StoneBlinkRoutine(GameObject stone)
    {
        MeshRenderer renderer = stone.GetComponentInChildren<MeshRenderer>();
        if (renderer == null) yield break;

        Color origColor = renderer.material.color;

        renderer.material.color = Color.white;
        yield return new WaitForSeconds(0.15f);

        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            renderer.material.color = Color.Lerp(Color.white, origColor, elapsed / 0.3f);
            yield return null;
        }
        renderer.material.color = origColor;
    }

    // =========================================================
    // [가성비 4] 유지형 오버레이 (안티매직용)
    // =========================================================
    public void SetAntiMagicOverlay(bool isOn, Color color = default)
    {
        if (antiMagicOverlayImage != null)
        {
            antiMagicOverlayImage.gameObject.SetActive(isOn);
            if (isOn) antiMagicOverlayImage.color = color;
        }
    }

    // =========================================================
    // [가성비 5] 유지형 오버레이 (칠죄종용)
    // =========================================================
    public void SetSevenSinsOverlay(bool isOn, Color color = default)
    {
        if (sevenSinsOverlayImage != null)
        {
            sevenSinsOverlayImage.gameObject.SetActive(isOn);
            if (isOn) sevenSinsOverlayImage.color = color;
        }
    }

    // =========================================================
    // 좌우 진동 (칼날비 드드드드 전용)
    // =========================================================
    public void PlayHorizontalShake(float duration = 0.5f, float magnitude = 0.3f, float speed = 60f)
    {
        StartCoroutine(HorizontalShakeRoutine(duration, magnitude, speed));
    }

    private IEnumerator HorizontalShakeRoutine(float duration, float magnitude, float speed)
    {
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
            // Sin 함수를 써서 좌우(X축)로만 미친듯이 왕복하게 만듦
            float x = Mathf.Sin(elapsed * speed) * magnitude;
            activeCam.transform.localPosition = originalPos + new Vector3(x, 0, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }
        activeCam.transform.localPosition = originalPos;
    }

    // =========================================================
    // 1. 칼날비 전용: 기존 좌우 셰이크 대신 상하좌우 다방향 셰이크로 교체
    // =========================================================
    public void PlayBladefallShake()
    {
        // 0.4초 동안 0.3의 강도로 강하게 사방으로 흔듦
        StartCoroutine(MultiDirectionShakeRoutine(0.4f, 0.3f));
    }

    // =========================================================
    // 2. 다중 점멸 (룰 파괴 3번 붉은 점멸용)
    // =========================================================
    public void PlayMultiFlash(Color color, int count, float duration)
    {
        StartCoroutine(MultiFlashRoutine(color, count, duration));
    }

    private IEnumerator MultiFlashRoutine(Color color, int count, float duration)
    {
        for (int i = 0; i < count; i++)
        {
            yield return StartCoroutine(ScreenFlashRoutine(color, duration / count));
        }
    }

    // =========================================================
    // 3. 신성화 연출 (노란빛 쌰랄라 -> 하얀 번쩍)
    // =========================================================
    public void PlayConsecrationVFX()
    {
        StartCoroutine(ConsecrationBurnRoutine());
    }

    private IEnumerator ConsecrationBurnRoutine()
    {
        if (consecrationOverlayImage == null) yield break;

        consecrationOverlayImage.gameObject.SetActive(true);

        // 1. 슈우우우... 기 모으기 (노란색으로 서서히 짙어짐)
        float buildUpTime = 1.0f;
        float elapsed = 0f;
        Color yellowLight = new Color(1f, 0.95f, 0.4f, 0f); // 연한 노란색

        while (elapsed < buildUpTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / buildUpTime;
            // 투명도 0 -> 0.7f 까지 기 모으는 중
            consecrationOverlayImage.color = new Color(yellowLight.r, yellowLight.g, yellowLight.b, t * 0.7f);
            yield return null;
        }

        // 2. 뜸 들이기 (살짝 멈춤 - 정적의 순간)
        yield return new WaitForSeconds(0.2f);

        // 3. 번쩍! (하얀색으로 확 바뀌며 100% 불투명)
        consecrationOverlayImage.color = Color.white;

        // 4. 스르륵 사라지기
        float fadeOutTime = 1.2f;
        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutTime;
            consecrationOverlayImage.color = new Color(1f, 1f, 1f, 1f - t);
            yield return null;
        }

        consecrationOverlayImage.gameObject.SetActive(false);
    }
    // =========================================================
    // 4. 유지형 오버레이 통합 관리
    // =========================================================
    public void SetPersistentOverlay(Image target, bool isOn, Color color = default)
    {
        if (target != null)
        {
            target.gameObject.SetActive(isOn);
            if (isOn) target.color = color;
        }
    }

    // =========================================================
    // 인스펙터 세팅값 그대로 키고 끄기만 하는 함수 (투명화용)
    // =========================================================
    public void ToggleOverlay(Image target, bool isOn)
    {
        if (target != null) target.gameObject.SetActive(isOn);
    }

    // =========================================================
    // 🚨 게임 종료 시 모든 스킬 연출 강제 종료 및 화면 클리어!
    // =========================================================
    public void ClearAllEffects()
    {
        // 1. 진행 중인 모든 카메라 진동, 점멸 코루틴 멱살 잡고 강제 정지!
        StopAllCoroutines();

        // 2. 카메라 원상복구 (흔들리거나 줌인된 상태로 멈춤 방지)
        Camera activeCam = Camera.main;
        if (cameraSwitcher != null)
        {
            activeCam = cameraSwitcher.topCamera.depth > 0 ? cameraSwitcher.topCamera :
                        (cameraSwitcher.blackPlayerCamera.depth > 0 ? cameraSwitcher.blackPlayerCamera : cameraSwitcher.whitePlayerCamera);
        }
        if (activeCam != null)
        {
            activeCam.fieldOfView = 60f; // 기본 FOV로 롤백
        }

        // 3. 켜져 있는 모든 오버레이 필터 자비 없이 강제 오프!
        SetPersistentOverlay(antiMagicOverlayImage, false);
        SetPersistentOverlay(sevenSinsOverlayImage, false);
        SetPersistentOverlay(invisibilityOverlayImage, false);
        SetPersistentOverlay(consecrationOverlayImage, false); // 🚨 방금 추가한 신성화 필터도 끔!

        if (screenOverlayImage != null) screenOverlayImage.gameObject.SetActive(false);
        if (screenIconImage != null) screenIconImage.gameObject.SetActive(false);

        Debug.Log("[SkillVFXManager] 게임 오버! 모든 스킬 이펙트 강제 클리어 완료!");
    }

    // =========================================================
    // [추가] 상하좌우 우다다다 진동 (X, Y, Z 축 모두 흔들림)
    // =========================================================
    public void PlayMultiDirectionShake(float duration = 0.5f, float magnitude = 0.2f)
    {
        StartCoroutine(MultiDirectionShakeRoutine(duration, magnitude));
    }

    private IEnumerator MultiDirectionShakeRoutine(float duration, float magnitude)
    {
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
            // 구체 내부의 랜덤한 좌표를 생성하여 상하좌우 무작위 진동 구현
            Vector3 randomOffset = Random.insideUnitSphere * magnitude;
            activeCam.transform.localPosition = originalPos + randomOffset;

            elapsed += Time.deltaTime;
            yield return null;
        }
        activeCam.transform.localPosition = originalPos;
    }
}