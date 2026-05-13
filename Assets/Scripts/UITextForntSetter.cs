#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class UITextFontSetter: EditorWindow
{
    private Font selectedUIFont;
    private TMP_FontAsset selectedTMPFont;
    private bool includeInactive = true;
    [MenuItem("CustomMenu/FontSetter 열기")]
    public static void OpenWindow()
    {
        GetWindow<UITextFontSetter>("Font Setter");
    }


    private void OnGUI()
    {
        GUILayout.Label("폰트 선택", EditorStyles.boldLabel);

        // 폰트를 드래그앤드롭 또는 클릭으로 선택
        // selectedUIFont = (Font)EditorGUILayout.ObjectField(
        //     "UI Text 폰트", selectedUIFont, typeof(Font), false);

        selectedTMPFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "TextMeshPro 폰트", selectedTMPFont, typeof(TMP_FontAsset), false);

        GUILayout.Space(10);

        // UI Text 교체 버튼
        // if (GUILayout.Button("UI Text 폰트 교체"))
        // {
        //     if (selectedUIFont == null)
        //     {
        //         Debug.LogError("UI Text 폰트를 선택해주세요.");
        //         return;
        //     }
        //     ChangeFontInUIText(selectedUIFont);
        // }

        // TextMeshPro 교체 버튼
        if (GUILayout.Button("TextMeshPro 폰트 교체"))
        {
            if (selectedTMPFont == null)
            {
                Debug.LogError("TextMeshPro 폰트를 선택해주세요.");
                return;
            }
            ChangeFontInTextMeshPro(selectedTMPFont);
        }
    }

    // private void ChangeFontInUIText(Font font)
    // {
    //     GameObject[] rootObj = GetSceneRootObjects();

    //     for (int i = 0; i < rootObj.Length; i++)
    //     {
    //         Component[] com = rootObj[i].GetComponentsInChildren(typeof(Text), true);
    //         foreach (Text txt in com)
    //         {   
    //             Undo.RecordObject(txt, "Change UIText Font");
    //             txt.font = font;
    //             EditorUtility.SetDirty(txt.gameObject);
    //         }
    //     }

    //     Debug.Log($"UIText 폰트 교체 완료 → {font.name}");
    // }

    private void ChangeFontInTextMeshPro(TMP_FontAsset fontAsset)
    {
        GameObject[] rootObj = GetSceneRootObjects();

        for (int i = 0; i < rootObj.Length; i++)
        {
            /// TextMeshProUGUI (UI용)
        TextMeshProUGUI[] comUI = rootObj[i].GetComponentsInChildren<TextMeshProUGUI>(includeInactive);
        foreach (TextMeshProUGUI txt in comUI)
        {
            Undo.RecordObject(txt, "Change TMP Font");
            Color prevColor = txt.color; // 기존 색상 저장
            txt.font = fontAsset;
            txt.color = prevColor;       // 교체 후 색상 복원
           
            EditorUtility.SetDirty(txt);
            if (PrefabUtility.IsPartOfPrefabInstance(txt.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(txt);
        }

        // TextMeshPro (3D용)
        TextMeshPro[] com3D = rootObj[i].GetComponentsInChildren<TextMeshPro>(includeInactive);
        foreach (TextMeshPro txt in com3D)
        {
            Undo.RecordObject(txt, "Change TMP Font");
            Color prevColor = txt.color; // 기존 색상 저장
            txt.font = fontAsset;
            txt.color = prevColor;       // 교체 후 색상 복원
            EditorUtility.SetDirty(txt);
            if (PrefabUtility.IsPartOfPrefabInstance(txt.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(txt);
        }
        }

        Debug.Log($"TextMeshPro 폰트 교체 완료 → {fontAsset.name}");
    }

    private static GameObject[] GetSceneRootObjects()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        return currentScene.GetRootGameObjects();
    }
}
#endif