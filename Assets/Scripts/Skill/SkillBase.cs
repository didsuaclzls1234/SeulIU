using UnityEngine;

public abstract class SkillBase
{
    public SkillData data;
    public int currentCooldown = 0; // 현재 남은 쿨타임
    public int remainingDuration = 0; // 현재 남은 지속 턴 (투명화, 안티매직 등)

    public SkillBase(SkillData skillData)
    {
        this.data = skillData;
    }

    // 조건 검사 (SP가 충분한가? 쿨타임이 도는가? 안티매직에 걸렸는가?)
    public virtual bool CanUse(int currentSP, bool isAntiMagicActive)
    {
        if (isAntiMagicActive && data.type != "전용") return false; // 패시브 제외 봉인
        if (currentSP < data.spCost) return false;
        if (currentCooldown > 0) return false;
        return true;
    }

    // 실제 효과 발동 (랜덤 좌표들은 매개변수로 받음)
    public abstract void Execute(int[] targetX, int[] targetY, GameManager gameManager, BoardManager board);

    // 턴이 끝날 때마다 호출되어 쿨타임과 지속시간을 줄임
    public virtual void OnTurnPassed()
    {
        if (currentCooldown > 0) currentCooldown--;
        if (remainingDuration > 0) remainingDuration--;
    }
}