using UnityEngine;
using System;

public class TimerManager : MonoBehaviour
{
    public GameManager gameManager;
    public GameHUD gameHUD;

    [Header("Settings")]
    public float skillSelectLimit = 60f; // 스킬 선택창 제한 시간
    public float turnLimit = 30f;        // 인게임 한 턴당 제한 시간 (예: 30초)
    private float currentTimer;
    private bool isTimerRunning = false;

    // 시간이 다 됐을 때 실행될 이벤트
    public event Action OnTimeOut;

    void Update()
    {
        // 솔로 모드이거나, 게임이 끝났거나, 타이머가 꺼져있으면 아예 연산하지 않음
        if (!isTimerRunning || gameManager.currentState == GameState.GameOver || gameManager.currentMode == PlayMode.Solo)
            return;

        currentTimer -= Time.deltaTime;

        // HUD에 시간 전달 (정수형으로 반환)
        if (gameHUD != null)
        {
            gameHUD.UpdateTimerUI(Mathf.Max(0, currentTimer));
        }

        if (currentTimer <= 0)
        {
            StopTimer();
            OnTimeOut?.Invoke();
        }
    }

    public void PauseTimer() => isTimerRunning = false;
    public void ResumeTimer() => isTimerRunning = true;

    // 턴을 아예 새로 시작할 때 (무르기 수락이나 턴 교체 시)
    public void RestartTurnTimer()
    {
        StopTimer();
        StartTurnTimer();
    }

    // 스킬 선택용 타이머 시작 함수 
    public void StartSkillSelectTimer()
    {
        if (gameManager.currentMode == PlayMode.Solo)
        {
            if (gameHUD != null) gameHUD.UpdateTimerUI(skillSelectLimit);
            return;
        }

        currentTimer = skillSelectLimit;
        isTimerRunning = true;
        Debug.Log($"[TimerManager] 스킬 선택 타이머 가동 ({skillSelectLimit}초)");
    }

    // 턴이 시작될 때 호출
    public void StartTurnTimer()
    {
        // 솔로 모드면 애초에 타이머를 켜지 않고 UI만 가득 찬 상태로 고정시킴
        if (gameManager.currentMode == PlayMode.Solo)
        {
            if (gameHUD != null) gameHUD.UpdateTimerUI(turnLimit);
            return;
        }

        currentTimer = turnLimit;
        isTimerRunning = true;
    }

    public void StopTimer()
    {
        isTimerRunning = false;
    }

    // ** 네트워크 동기화 시 사용할 서버 시간 보정용 함수  -> 네트워크 동기화 시 이거 사용하세요!!
    public void SyncTimerFromServer(float remainingTime)
    {
        currentTimer = remainingTime;
    }
}