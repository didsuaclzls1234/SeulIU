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
        // targetX[0], targetY[0] 에는 내가 마우스로 클릭한 좌표가 들어있음
        int tx = targetX[0];
        int ty = targetY[0];

        // 1. 첫 번째 돌(선택한 돌) 검증 및 삭제
        if (!IsValidTarget(tx, ty, gameManager, board)) return false;

        // 시전자의 화면에서 이 돌이 투명(MeshRenderer.enabled == false)인지 확인
        GameObject targetObj1 = board.GetStoneObjectAt(tx, ty);
        StoneColor target1Color = (StoneColor)board.grid[tx, ty];
        bool isVisibleToCaster1 = false;

        if (targetObj1 != null)
        {
            // 자식들에 있는 렌더러들을 다 가져와서 첫 번째 놈의 상태를 확인
            Renderer[] renderers1 = targetObj1.GetComponentsInChildren<Renderer>();
            if (renderers1.Length > 0) isVisibleToCaster1 = renderers1[0].enabled;
        }

        // 데이터 및 오브젝트 삭제
        board.grid[tx, ty] = 0;
        board.RemoveStoneObjectAt(tx, ty);

        // 보일 때만 깜빡임 (투명한 돌을 찍었다면 아무 연출 없이 사라짐)
        if (isVisibleToCaster1)
        {
            board.BlinkEmptySpaceEffect(tx, ty, Color.red, target1Color);
        }

        // -------------------------------------------------------------------------------
        // 2. 두 번째 돌(랜덤 돌) 찾기
        int enemyColorInt = (gameManager.currentTurnColor == StoneColor.Black) ? 2 : 1;
        List<Vector2Int> otherEnemyStones = new List<Vector2Int>();

        for (int x = 0; x < board.boardSize; x++)
        {
            for (int y = 0; y < board.boardSize; y++)
            {
                // 상대방 돌이면서, 내가 방금 지운 좌표가 아닌 것, 보호막(shieldGrid)이 없는 돌만 수집
                if (board.grid[x, y] == enemyColorInt && !(x == tx && y == ty) && !board.shieldGrid[x, y])
                {
                    otherEnemyStones.Add(new Vector2Int(x, y));
                }
            }
        }

        // 3. 랜덤 돌 제거 (상대 돌이 더 있다면)
        if (otherEnemyStones.Count > 0)
        {
            int randomIndex = Random.Range(0, otherEnemyStones.Count);
            Vector2Int randomPos = otherEnemyStones[randomIndex];

            // 랜덤으로 뽑힌 두 번째 돌도 투명 상태인지 확인
            // --- [2. 두 번째(랜덤) 돌 투명 상태 확인 수정] ---
            GameObject targetObj2 = board.GetStoneObjectAt(randomPos.x, randomPos.y);
            StoneColor target2Color = (StoneColor)board.grid[randomPos.x, randomPos.y];
            bool isVisibleToCaster2 = false;

            if (targetObj2 != null)
            {
                Renderer[] renderers2 = targetObj2.GetComponentsInChildren<Renderer>();
                if (renderers2.Length > 0) isVisibleToCaster2 = renderers2[0].enabled;
            }

            // 데이터 및 오브젝트 삭제
            board.grid[randomPos.x, randomPos.y] = 0;
            board.RemoveStoneObjectAt(randomPos.x, randomPos.y);

            // 랜덤 돌도 보일 때만 깜빡임!
            if (isVisibleToCaster2)
            {
                board.BlinkEmptySpaceEffect(randomPos.x, randomPos.y, Color.red, target2Color);
            }

            // 랜덤으로 고른 좌표를 배열의 1번 인덱스에 저장 (네트워크 전송용)
            // 호출부인 SkillManager에서 이 배열을 그대로 가져다 씁니다.
            targetX[1] = randomPos.x;
            targetY[1] = randomPos.y;

            Debug.Log($"[SkillErase] 선택 파괴:({tx},{ty}), 랜덤 파괴:({randomPos.x},{randomPos.y})");
        }
        else
        {
            // 상대 돌이 하나뿐이라 랜덤 제거를 못 할 경우 -1로 표기
            targetX[1] = -1;
            targetY[1] = -1;
            Debug.Log($"[SkillErase] 선택 파괴:({tx},{ty}), 추가로 제거할 상대 돌이 없습니다.");
        }

        return true; // true 시에만 sp 깎임
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