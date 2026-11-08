using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class SpotLightController : MonoBehaviour
{
    [Header("位置")]
    public Vector3 lightPosition = new Vector3(0, 5, 0);

    [Header("向き")]
    [Range(0f, 360f)]
    public float lightRotation = 0f;

    [Header("距離")]
    [Range(0.1f, 20f)]
    public float lightDistance = 5f;

    [Header("角度")]
    [Range(1f, 170f)]
    public float lightAngle = 60f;

    [Header("光の強さ")]
    [Range(0f, 5f)]
    public float lightIntensity = 1.5f;

    [Header("先端の円")]
    public Transform lightCircle;

    private Light2D spotLight;

    void Awake()
    {
        spotLight = GetComponent<Light2D>();
        ApplySettings();
    }

    void OnValidate()
    {
        spotLight = GetComponent<Light2D>();

        if (spotLight != null)
        {
            ApplySettings();
        }
    }

    void ApplySettings()
    {
        // ライトの位置
        transform.position = lightPosition;

        // ライトの向き
        transform.rotation =
            Quaternion.Euler(0f, 0f, lightRotation);

        // ライトの強さ
        spotLight.intensity = lightIntensity;

        // ライトの照射距離
        spotLight.pointLightOuterRadius = lightDistance;

        // ライトの角度
        spotLight.pointLightOuterAngle = lightAngle;

        // 先端の円
        if (lightCircle != null)
        {
            // ライトの向いている方向
            Vector3 direction = transform.up;

            // 円の位置
            lightCircle.position =
                transform.position + direction * lightDistance;

            // 角度と距離から円の直径を計算
            float size =
                2f * lightDistance *
                Mathf.Tan(lightAngle * 0.5f * Mathf.Deg2Rad);

            // 円のサイズを変更
            lightCircle.localScale =
                new Vector3(size, size, 1f);
        }
    }
}