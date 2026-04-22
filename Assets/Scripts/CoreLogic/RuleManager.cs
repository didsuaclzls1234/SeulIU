using UnityEngine;

// 각 플레이어별 상태를 독립적으로 관리하는 구조체 (스킬 버프/디버프 적용에 최적화)
[System.Serializable]
public class RuleSettings
{
    public bool ban33 = false;
    public bool ban44 = false;
    public bool banOverline = false; // 장목 금지 여부

    // * 승리 조건도 플레이어마다 개별로 가짐 (스킬로 내 승리조건만 4목으로 줄일 수도 있음)
    public int winCondition = 5;
}

public class RuleManager : MonoBehaviour
{
    public RuleSettings blackRules = new RuleSettings();
    public RuleSettings whiteRules = new RuleSettings();

    public int currentWinCondition = 5; // ** 평소엔 5목. 나중에 스킬 발동 시 6으로 변경가능하도록 확장성 고려

    public void Start()
    {
        // 테스트용: 시작할 때 렌주 룰(표준 룰)로 세팅
        SetPreset_Renju();
    }

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

    // 2. 금수 자리인지 검사하는 함수 (BoardManager가 돌을 놓기 전에 확인함)
    public bool IsForbiddenMove(int x, int y, int playerType, int[,] grid, int boardSize, bool silent = false) // silent는 로그 출력 제어를 위해 추가함.
    {
        RuleSettings currentRules = (playerType == 1) ? blackRules : whiteRules;
        int currentWin = currentRules.winCondition;

        string[] lines = GetLines(x, y, playerType, grid, boardSize, currentWin);

        if (currentRules.banOverline && CheckOverline(lines, currentWin))
        {
            if (!silent) Debug.Log($"❌ 장목({currentWin + 1}목 이상) 금수 자리입니다!");
            return true;
        }

        if (currentRules.ban44 && CheckDynamicRule(lines, currentWin, currentWin - 1, 2))
        {
            if (!silent) Debug.Log($"❌ 쌍{currentWin - 1} 금수 자리입니다!");
            return true;
        }

        if (currentRules.ban33 && CheckDynamicRule(lines, currentWin, currentWin - 2, 2))
        {
            if (!silent) Debug.Log($"❌ 쌍{currentWin - 2} 금수 자리입니다!");
            return true;
        }

        return false;
    }

    // ----------------------------------------------------
    // 금수 검사 알고리즘

    // 4방향 탐색 함수
    private string[] GetLines(int x, int y, int playerType, int[,] grid, int boardSize, int targetWinCondition)
    {
        int[,] dirs = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } }; // 가로, 세로, 우상향, 우하향
        string[] lines = new string[4];

        for (int i = 0; i < 4; i++)
        {
            lines[i] = ExtractLine(x, y, dirs[i, 0], dirs[i, 1], playerType, grid, boardSize, targetWinCondition);
        }
        return lines;
    }

    private string ExtractLine(int x, int y, int dx, int dy, int playerType, int[,] grid, int size, int targetWinCondition)
    {
        string result = "";

        // 핵심: 시야(range)를 동적으로 확장.
        // 장목(Overline)이나 양쪽 끝이 비어있는지까지 감지하려면, 목표 개수보다 1칸 더 멀리 봐야 함
        int range = targetWinCondition + 1;

        for (int i = -range; i <= range; i++)
        {
            int nx = x + dx * i;
            int ny = y + dy * i;

            if (nx < 0 || ny < 0 || nx >= size || ny >= size) result += "2";
            else if (grid[nx, ny] == 0) result += "0";
            else if (grid[nx, ny] == playerType) result += "1";
            else result += "2";
        }

        // 정중앙 인덱스도 항상 range와 동일해짐 (예: range가 6이면 인덱스 6이 정중앙)
        char[] chars = result.ToCharArray();
        chars[range] = '1';
        return new string(chars);
    }

    // 장목(Overline) 동적 검사기
    private bool CheckOverline(string[] lines, int targetWinCondition)
    {
        // 5목 룰이면 "111111" (6개) 패턴을 찾음, 6목 룰이면 "1111111" (7개) 패턴을 찾음
        string overlinePattern = new string('1', targetWinCondition + 1);

        foreach (string line in lines)
        {
            if (line.Contains(overlinePattern)) return true;
        }
        return false;
    }

    // (N목, 쌍삼, 쌍사 등을 동적으로 모두 잡아낼 수 있음)
    private bool CheckDynamicRule(string[] lines, int targetWinCondition, int targetStones, int requiredWindows)
    {
        int totalMatches = 0;

        foreach (string line in lines)
        {
            // 예: 6목 스킬 발동 중이면, 창문 크기는 6
            int windowSize = targetWinCondition;

            // 3-3 (열린 3) 같은 경우는 양쪽 끝이 비어있어야 하므로 창문 크기를 1칸 더 넓게 봄
            if (targetStones == targetWinCondition - 2) windowSize += 1;

            // 문자열(line) 위로 창문을 한 칸씩 슬라이딩
            for (int i = 0; i <= line.Length - windowSize; i++)
            {
                string window = line.Substring(i, windowSize);

                // 창문 안에 상대 돌(2)이나 벽(2)이 있으면 이 창문은 무효 (막힌 길)
                if (window.Contains("2")) continue;

                // 창문 안에서 내 돌(1)의 개수를 세기
                int myStoneCount = window.Split('1').Length - 1; // (1의 개수 카운트)

                // 목표한 돌의 개수(예: 3-3이면 3개, 4-4면 4개)와 일치하는가?
                if (myStoneCount == targetStones)
                {
                    totalMatches++;
                    break; // 이 줄(축)에서는 하나 찾았으니 다음 축으로 넘어감
                }
            }
        }

        // 찾아낸 패턴의 개수가 요구치(쌍사면 2개) 이상인가?
        return totalMatches >= requiredWindows;
    }

}