using System.Collections.Generic;
using UnityEngine;

public class Skill_5_Erase : SkillBase
{
    // 생성자 (부모 클래스의 생성자로 데이터를 넘겨줌)
    public Skill_5_Erase(SkillData skillData) : base(skillData)
    {
    }

    // 1. 스킬 버튼 누를 때 검사 로직 덮어쓰기
    public override SkillUseResult CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        SkillUseResult baseResult = base.CanUse(currentSP, isAntiMagicActive, board, myColor);
        if (baseResult != SkillUseResult.Success) return baseResult;

        // 보드판을 스캔해서 상대방 돌이 하나라도 있는지 확인
        bool hasEnemyStone = false;
        int enemyColorInt = (myColor == StoneColor.Black) ? 2 : 1; // 1:흑, 2:백

        for (int x = 0; x < board.boardSize; x++)
        {
            for (int y = 0; y < board.boardSize; y++)
            {
                if (board.grid[x, y] == enemyColorInt)
                {
                    hasEnemyStone = true;
                    break;
                }
            }
            if (hasEnemyStone) break;
        }

        if (!hasEnemyStone)
        {
            Debug.LogWarning("[SkillErase] 바둑판에 제거할 상대방 돌이 없습니다!");
            return SkillUseResult.NoValidTarget; // 스킬 사용 불가!
        }

        return SkillUseResult.Success;
    }

    // 5번 스킬(제거)의 고유 발동 로직
    // - 실제 마우스로 클릭했을 때 검사
    public override bool Execute(int[] targetX, int[] targetY, GameManager gameManager, BoardManager board)
    {
        int tx = targetX[0];
        int ty = targetY[0];

        if (!IsValidTarget(tx, ty, gameManager, board)) return false;

        // 1. 즉시 삭제하지 않고 SkillManager에 좌표 '예약'만 걸어둠
        if (gameManager.skillManager != null)
        {
            gameManager.skillManager.pendingRemoveTarget = new Vector2Int(tx, ty);
        }

        // 2. 네트워크 전송용 배열 정리
        if (targetX.Length > 1)
        {
            targetX[1] = -1;
            targetY[1] = -1;
        }

        Debug.Log($"[SkillErase] 선택 파괴 예약 완료:({tx},{ty}) - 일반 착수 시 파괴됩니다.");

        return true; // true 시에만 SP 깎임
    }

    private bool IsValidTarget(int x, int y, GameManager gm, BoardManager board)
    {
        if (x < 0 || x >= board.boardSize || y < 0 || y >= board.boardSize) return false;
        if (board.grid[x, y] == 0) return false; // 빈 칸
        if (board.grid[x, y] == (int)gm.currentTurnColor) return false; // 내 돌
        if (board.shieldGrid[x, y])
        {
            Debug.LogWarning("[Erase] 신의 가호로 보호받는 돌은 제거할 수 없습니다!");
            return false;
        }
        return true;
    }

}