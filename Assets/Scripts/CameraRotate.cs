using UnityEngine;

public class CameraRotate : MonoBehaviour
{
    public float rotateSpeed = 100f;

    private float yaw;   // 좌우
    private float pitch; // 상하

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        // 좌우 (A/D)
        float horizontal = 0f;
        if (Input.GetKey(KeyCode.A)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D)) horizontal = 1f;

        // 상하 (W/S)
        float vertical = 0f;
        if (Input.GetKey(KeyCode.W)) vertical = -1f;
        if (Input.GetKey(KeyCode.S)) vertical = 1f;

        yaw += horizontal * rotateSpeed * Time.deltaTime;
        pitch += vertical * rotateSpeed * Time.deltaTime;

        // 위아래 제한 
        pitch = Mathf.Clamp(pitch, -60f, 60f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}