using UnityEngine;

// 각 플레이어별 상태를 독립적으로 관리하는 구조체 (스킬 버프/디버프 적용에 최적화)
[System.Serializable]
public class RuleSettings
{
    public bool ban33 = false;
    public bool ban44 = false;
    public bool banOverline = false; // 장목 금지 여부

    // 승리 조건도 플레이어마다 개별로 가짐 (스킬 확장성 핵심)
    public int winCondition = 5;
}

public class RuleManager : MonoBehaviour
{
    public RuleSettings blackRules = new RuleSettings();
    public RuleSettings whiteRules = new RuleSettings();

    // * 4방향 탐색 캐싱용 벡터 (가로, 세로, 우상향, 우하향)
    private readonly int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };

    public void Start()
    {
        // 테스트용: 시작할 때 렌주 룰(표준 룰)로 세팅
        SetPreset_Renju();
    }

    //  외부에서 현재 플레이어의 WinCondition 물어볼 때 대답해주는 헬퍼 함수
    public int GetWinCondition(int playerType)
    {
        return (playerType == 1) ? blackRules.winCondition : whiteRules.winCondition;
    }

    // ------------------------------------------

    // 1. 룰 프리셋 (UI 버튼이나 스킬로 호출하면 됨)
    public void SetPreset_Renju() // (1) 렌주 룰: 흑돌만 3-3, 4-4, 장목 금지
    {
        blackRules.ban33 = true; blackRules.ban44 = true; blackRules.banOverline = true;
        whiteRules.ban33 = false; whiteRules.ban44 = false; whiteRules.banOverline = false;
        Debug.Log("[RuleManager] 렌주 룰(표준) 적용 완료!");
    }

    public void SetPreset_Korean() // (2) 일반 오목(한국식): 흑/백 모두 3-3만 금지. 4-4, 장목은 허용(or 장목은 승리로 안 쳐줌)
    {
        blackRules.ban33 = true; blackRules.ban44 = false; blackRules.banOverline = false;
        whiteRules.ban33 = true; whiteRules.ban44 = false; whiteRules.banOverline = false;
        Debug.Log("[RuleManager] 일반 오목(쌍삼만 금지) 룰 적용 완료!");
    }

    public void SetPreset_Free() // (3) 자유 오목: 흑/백 모두 금지사항 없음. 자유롭게 두기
    {
        blackRules.ban33 = false; blackRules.ban44 = false; blackRules.banOverline = false;
        whiteRules.ban33 = false; whiteRules.ban44 = false; whiteRules.banOverline = false;
    }

    // * 외부(BoardManager)에서 찌르는 진입점
    public bool IsForbiddenMove(int x, int y, int playerType, int[,] grid, int boardSize, bool silent = false)
    {
        RuleSettings currentRules = (playerType == 1) ? blackRules : whiteRules;

        // 금수 룰이 아예 없으면 즉시 통과
        if (!currentRules.ban33 && !currentRules.ban44 && !currentRules.banOverline) return false;

        // 코어 엔진으로 검사 시작 (최초 호출이므로 depth는 0)
        return CheckIsForbidden(x, y, playerType, grid, boardSize, currentRules, depth: 0, silent: silent);
    }

    // ----------------------------------------------------
    // GC-Free 재귀 백트래킹 코어 알고리즘

    // 실제 금수 여부를 딥 다이브해서 판별하는 사령탑
    public bool CheckIsForbidden(int x, int y, int player, int[,] grid, int size, RuleSettings rules, int depth, bool silent)
    {
        int W = rules.winCondition;

        // 1. 승리(5목) 우선의 법칙: 지금 둬서 목표 달성 가능하면, 금수 자리였어도 그 자리에 무조건 둘 수 있음
        if (IsWin(x, y, player, grid, size, W, rules.banOverline)) return false;

        // * 백트래킹: 바둑판에 가상으로 돌을 놓음
        grid[x, y] = player;
        bool isForbidden = false;

        // 2. 장목 (Overline) 검사
        if (rules.banOverline && IsOverline(x, y, player, grid, size, W))
        {
            if (!silent && depth == 0) Debug.Log($"❌ 장목({W + 1}목 이상) 금수 자리입니다!");
            isForbidden = true;
        }
        // 3. 쌍사 (4-4) 검사
        else if (rules.ban44)
        {
            int count4 = 0;
            for (int d = 0; d < 4; d++)
            {
                if (CountWinningSpotsOnAxis(x, y, dirs[d, 0], dirs[d, 1], player, grid, size, W, rules.banOverline) >= 1) count4++;
            }

            if (count4 >= 2)
            {
                if (!silent && depth == 0) Debug.Log($"❌ 쌍{W - 1} 금수 자리입니다!");
                isForbidden = true;
            }
        }

        // 4. 쌍삼 (3-3) 검사 (* 여기서 4-3-3이 정확히 count3 >= 2로 잡힘!)
        // depth <= 1 조건: 거짓 3 판별을 위해 딱 한 번만 더 파고들게 제어
        if (!isForbidden && rules.ban33 && depth <= 1)
        {
            int count3 = 0;
            for (int d = 0; d < 4; d++)
            {
                if (CheckOpen3OnAxis(x, y, dirs[d, 0], dirs[d, 1], player, grid, size, W, rules, depth)) count3++;
            }

            if (count3 >= 2)
            {
                if (!silent && depth == 0) Debug.Log($"❌ 쌍{W - 2} 금수 자리입니다!");
                isForbidden = true;
            }
        }

        // * 백트래킹: 가상 돌 제거 (원상복구)
        grid[x, y] = 0;
        return isForbidden;
    }

    private bool IsWin(int x, int y, int player, int[,] grid, int size, int W, bool banOverline)
    {
        for (int d = 0; d < 4; d++)
        {
            int count = 1 + CountDir(x, y, dirs[d, 0], dirs[d, 1], player, grid, size) + CountDir(x, y, -dirs[d, 0], -dirs[d, 1], player, grid, size);
            if (count == W) return true;
            if (!banOverline && count > W) return true;
        }
        return false;
    }

    private bool IsOverline(int x, int y, int player, int[,] grid, int size, int W)
    {
        for (int d = 0; d < 4; d++)
        {
            int count = 1 + CountDir(x, y, dirs[d, 0], dirs[d, 1], player, grid, size) + CountDir(x, y, -dirs[d, 0], -dirs[d, 1], player, grid, size);
            if (count > W) return true;
        }
        return false;
    }

    // 특정 축에서 빈칸(0)을 하나 채웠을 때 목표(W)를 달성하는 '승리 스팟'이 몇 개인지 카운트
    private int CountWinningSpotsOnAxis(int x, int y, int dx, int dy, int player, int[,] grid, int size, int W, bool banOverline)
    {
        int winningSpots = 0;
        for (int i = -W; i <= W; i++)
        {
            if (i == 0) continue;
            int nx = x + i * dx;
            int ny = y + i * dy;

            if (nx >= 0 && nx < size && ny >= 0 && ny < size && grid[nx, ny] == 0)
            {
                int rightCount = CountDir(nx, ny, dx, dy, player, grid, size);
                int leftCount = CountDir(nx, ny, -dx, -dy, player, grid, size);
                int count = 1 + rightCount + leftCount;

                if ((count == W) || (!banOverline && count > W))
                {
                    // 그 승리 라인이 우리가 방금 놓은 돌(x, y)과 연결되어 있는가?
                    if (i > 0 && leftCount >= i) winningSpots++;
                    else if (i < 0 && rightCount >= -i) winningSpots++;
                }
            }
        }
        return winningSpots;
    }

    // 재귀를 활용하여 '진짜 열린 3'인지 판별
    private bool CheckOpen3OnAxis(int x, int y, int dx, int dy, int player, int[,] grid, int size, int W, RuleSettings rules, int depth)
    {
        for (int i = -W; i <= W; i++)
        {
            if (i == 0) continue;
            int nx = x + i * dx;
            int ny = y + i * dy;

            if (nx >= 0 && nx < size && ny >= 0 && ny < size && grid[nx, ny] == 0)
            {
                // 1차 필터링: 빈칸(nx, ny)에 돌을 놓았을 때 원래 돌(x, y)과 이어지는가?
                grid[nx, ny] = player;
                int right = CountDir(nx, ny, dx, dy, player, grid, size);
                int left = CountDir(nx, ny, -dx, -dy, player, grid, size);
                grid[nx, ny] = 0;

                bool isConnected = (i > 0 && left >= i) || (i < 0 && right >= -i);
                if (!isConnected) continue;

                // * 거짓 3 필터링: 그 빈칸(nx, ny)이 금수 자리라면 3으로 인정하지 않음!
                if (CheckIsForbidden(nx, ny, player, grid, size, rules, depth + 1, silent: true))
                    continue;

                // 그 빈칸에 돌을 놨을 때, '열린 4(승리 스팟이 2개 이상)'가 되는가?
                grid[nx, ny] = player;
                int winSpots = CountWinningSpotsOnAxis(nx, ny, dx, dy, player, grid, size, W, rules.banOverline);
                grid[nx, ny] = 0;

                if (winSpots >= 2) return true;
            }
        }
        return false;
    }

    // 특정 방향으로 연속된 내 돌의 개수를 세는 순수 포인터 함수
    private int CountDir(int x, int y, int dx, int dy, int player, int[,] grid, int size)
    {
        int count = 0;
        int nx = x + dx;
        int ny = y + dy;
        while (nx >= 0 && nx < size && ny >= 0 && ny < size && grid[nx, ny] == player)
        {
            count++;
            nx += dx;
            ny += dy;
        }
        return count;
    }

    // 이 함수를 호출하면 그 순간의 룰 상태를 '복사(Clone)'해서 던져줌!
    public RuleSettings GetRulesSnapshot(int playerType)
    {
        RuleSettings target = (playerType == 1) ? blackRules : whiteRules;
        return new RuleSettings
        {
            ban33 = target.ban33,
            ban44 = target.ban44,
            banOverline = target.banOverline,
            winCondition = target.winCondition
        };
    }
}