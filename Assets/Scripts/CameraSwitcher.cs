using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera topCamera;
    public Camera playerCamera;

    private bool isTopView = true;

    void Start()
    {
        SetTopView(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isTopView = !isTopView;
            SetTopView(isTopView);
        }
    }

    void SetTopView(bool value)
    {
        topCamera.gameObject.SetActive(value);
        playerCamera.gameObject.SetActive(!value);
    }
}