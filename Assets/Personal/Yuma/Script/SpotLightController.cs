
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
///     スポットライトの位置・向き・距離・角度・強さを制御する。
/// </summary>
[RequireComponent(typeof(Light2D))]
public class SpotLightController : MonoBehaviour
{
    [Header("位置")]
    [SerializeField, Tooltip("ライトのワールド座標。")]
    private Vector3 _lightPosition = new Vector3(0f, 5f, 0f);

    [Header("向き")]
    [SerializeField, Range(0f, 360f), Tooltip("ライトの回転角度。")]
    private float _lightRotation;

    [Header("距離")]
    [SerializeField, Range(0.1f, 20f), Tooltip("ライトの照射距離。")]
    private float _lightDistance = 5f;

    [Header("角度")]
    [SerializeField, Range(1f, 170f), Tooltip("ライトの照射角度。")]
    private float _lightAngle = 60f;

    [Header("光の強さ")]
    [SerializeField, Range(0f, 5f), Tooltip("ライトの明るさ。")]
    private float _lightIntensity = 1.5f;

    private Light2D _spotLight;

    /// <summary>
    ///     ライトの参照を取得して設定を反映する。
    /// </summary>
    private void Awake()
    {
        _spotLight = GetComponent<Light2D>();
        ApplySettings();
    }

    /// <summary>
    ///     Inspectorで設定値が変更されたときにライトへ反映する。
    /// </summary>
    private void OnValidate()
    {
        if (!TryGetComponent<Light2D>(out _spotLight))
        {
            return;
        }

        ApplySettings();
    }

    /// <summary>
    ///     Inspectorで設定した5項目をライトに反映する。
    /// </summary>
    private void ApplySettings()
    {
        if (_spotLight == null)
        {
            return;
        }

        // ライトの位置を設定する。
        transform.position = _lightPosition;

        // ライトの向きを設定する。
        transform.rotation =
            Quaternion.Euler(0f, 0f, _lightRotation);

        // ライトの強さを設定する。
        _spotLight.intensity = _lightIntensity;

        // ライトの照射距離を設定する。
        _spotLight.pointLightOuterRadius = _lightDistance;

        // ライトの照射角度を設定する。
        _spotLight.pointLightOuterAngle = _lightAngle;
    }
}
