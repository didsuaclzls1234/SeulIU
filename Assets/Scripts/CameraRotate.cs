using UnityEngine;

public class CameraRotate : MonoBehaviour
{
    public float rotateSpeed = 100f;

    private float baseYaw;
    private float basePitch;

    private float yawOffset;   // 좌우 오프셋
    private float pitchOffset; // 상하 오프셋

    public float yawLimit = 60f;
    public float pitchLimit = 45f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;

        baseYaw = angles.y;
        basePitch = angles.x;

        yawOffset = 0f;
        pitchOffset = 0f;
    }

    void Update()
    {
        float horizontal = 0f;
        if (Input.GetKey(KeyCode.A)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D)) horizontal = 1f;

        float vertical = 0f;
        if (Input.GetKey(KeyCode.W)) vertical = -1f;
        if (Input.GetKey(KeyCode.S)) vertical = 1f;

        // 오프셋 누적
        yawOffset += horizontal * rotateSpeed * Time.deltaTime;
        pitchOffset += vertical * rotateSpeed * Time.deltaTime;

        // Clamp (오프셋 기준!)
        yawOffset = Mathf.Clamp(yawOffset, -yawLimit, yawLimit);
        pitchOffset = Mathf.Clamp(pitchOffset, -pitchLimit, pitchLimit);

        // 최종 회전 = 기준 + 오프셋
        float finalYaw = baseYaw + yawOffset;
        float finalPitch = basePitch + pitchOffset;

        transform.rotation = Quaternion.Euler(finalPitch, finalYaw, 0f);
    }
}