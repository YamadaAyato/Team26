
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
///     上方から照らすFreeformライトと照射範囲を制御する。
/// </summary>
public class OverheadLightController : MonoBehaviour
{
    private const int ELLIPSE_SEGMENT_COUNT = 32;

    [Header("実際のライト")]
    [SerializeField] private Light2D _overheadLight;

    [Header("地面の照射範囲")]
    [SerializeField] private Transform _lightArea;

    [Header("移動設定")]
    [SerializeField, Tooltip("左右の移動距離。")]
    private float _moveDistance = 3f;

    [SerializeField, Tooltip("ライトが往復する速さ。")]
    private float _oscillationSpeed = 1f;

    [Header("照射範囲のサイズ")]
    [SerializeField, Min(0.1f), Tooltip("照射範囲の横幅。")]
    private float _lightWidth = 3f;

    [SerializeField, Min(0.1f), Tooltip("照射範囲の高さ。")]
    private float _lightHeight = 3f;

    private Vector3 _startPosition;
    private float _elapsedTime;

    /// <summary>
    ///     Inspectorの設定変更を反映する。
    /// </summary>
    private void OnValidate()
    {
        if (_lightArea == null || _overheadLight == null)
        {
            return;
        }

        ApplyLightSize();
    }

    /// <summary>
    ///     最初に配置された位置を移動の基準として保存する。
    /// </summary>
    private void Awake()
    {
        _startPosition = transform.position;
        _elapsedTime = 0f;
    }

    /// <summary>
    ///     ライトの参照を確認して照射範囲を設定する。
    /// </summary>
    private void Start()
    {
        if (_lightArea == null || _overheadLight == null)
        {
            Debug.LogError(
                $"[{nameof(OverheadLightController)}] ライトまたはLightAreaが設定されていません。",
                this);

            enabled = false;
            return;
        }

        ApplyLightSize();
    }

    /// <summary>
    ///     ライト自身の経過時間を使って親オブジェクトを左右に移動させる。
    /// </summary>
    private void Update()
    {
        _elapsedTime += Time.deltaTime;

        float x =
            Mathf.Sin(_elapsedTime * _oscillationSpeed) * _moveDistance;

        transform.position =
            _startPosition + new Vector3(x, 0f, 0f);
    }

    /// <summary>
    ///     楕円形のFreeformライトを生成する。
    /// </summary>
    private void ApplyLightSize()
    {
        _lightArea.localScale =
            new Vector3(_lightWidth, _lightHeight, 1f);

        Vector3[] shapePath = new Vector3[ELLIPSE_SEGMENT_COUNT];

        float radiusX = _lightWidth * 0.5f;
        float radiusY = _lightHeight * 0.5f;

        for (int i = 0; i < ELLIPSE_SEGMENT_COUNT; i++)
        {
            float angle =
                2f * Mathf.PI * i / ELLIPSE_SEGMENT_COUNT;

            shapePath[i] = new Vector3(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY,
                0f);
        }

        _overheadLight.SetShapePath(shapePath);
    }
}
