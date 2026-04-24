[System.Serializable]
public struct SkillData
{
    public int skillId;
    public string skillName;
    public string type; // "특수형", "공격형", "전용" 등
    public int spCost;
    public int cooldown;
    public int durationTurn; // 지속 턴 (없으면 0)
}
