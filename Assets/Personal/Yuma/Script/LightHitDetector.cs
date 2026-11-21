
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
///     横向きライトがプレイヤーに届いているか判定する。
/// </summary>
public class LightHitDetector : MonoBehaviour
{
    private const int SAMPLE_COUNT = 21;

    [Header("判定範囲")]
    [SerializeField, Tooltip("プレイヤー全体の警告判定範囲。")]
    private PolygonCollider2D _bodyWarningArea;

    [SerializeField, Tooltip("プレイヤーの中心の死亡判定範囲。")]
    private CircleCollider2D _centerDeathArea;

    [Header("ライト設定")]
    [SerializeField, Tooltip("判定対象の横向きライト。")]
    private Light2D[] _spotLights;

    [Header("遮光設定")]
    [SerializeField, Tooltip("光を遮る壁や家具のレイヤー。")]
    private LayerMask _obstacleLayer;

    private bool _isCenterHit;
    private bool _isWarning;

    /// <summary>
    ///     毎フレーム、プレイヤーへの照射を確認する。
    /// </summary>
    private void Update()
    {
        // 身体の中心に光が当たっているか確認する。
        if (IsCenterIlluminated())
        {
            if (!_isCenterHit)
            {
                _isCenterHit = true;
                Death();
            }

            _isWarning = false;
            return;
        }

        // 中心に光が当たらなくなったらリセットする。
        _isCenterHit = false;

        // 身体の端に光が当たっているか確認する。
        bool edgeHit = IsColliderIlluminated(_bodyWarningArea);

        // 端に光が当たった瞬間だけ警告を出す。
        if (edgeHit && !_isWarning)
        {
            _isWarning = true;
            Warning();
        }
        else if (!edgeHit)
        {
            _isWarning = false;
        }
    }

    /// <summary>
    ///     プレイヤーの中心にライトが届いているか判定する。
    /// </summary>
    /// <returns>ライトが届いていればtrue。</returns>
    public bool IsCenterIlluminated()
    {
        return IsColliderIlluminated(_centerDeathArea);
    }

    /// <summary>
    ///     指定したColliderの範囲にライトが届いているか判定する。
    /// </summary>
    /// <param name="targetCollider">判定するCollider。</param>
    /// <returns>ライトが届いている範囲があればtrue。</returns>
    private bool IsColliderIlluminated(Collider2D targetCollider)
    {
        if (targetCollider == null || !targetCollider.enabled)
        {
            return false;
        }

        Bounds bounds = targetCollider.bounds;

        for (int x = 0; x <= SAMPLE_COUNT; x++)
        {
            for (int y = 0; y <= SAMPLE_COUNT; y++)
            {
                float positionX = Mathf.Lerp(
                    bounds.min.x,
                    bounds.max.x,
                    (float)x / SAMPLE_COUNT
                );

                float positionY = Mathf.Lerp(
                    bounds.min.y,
                    bounds.max.y,
                    (float)y / SAMPLE_COUNT
                );

                Vector2 samplePosition = new Vector2(
                    positionX,
                    positionY
                );

                // 実際のColliderの内側にある点だけを調べる。
                if (!targetCollider.OverlapPoint(samplePosition))
                {
                    continue;
                }

                if (IsPointIlluminated(samplePosition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     指定した位置にいずれかのライトが届いているか判定する。
    /// </summary>
    /// <param name="targetPosition">判定する位置。</param>
    /// <returns>ライトが届いていればtrue。</returns>
    private bool IsPointIlluminated(Vector2 targetPosition)
    {
        if (_spotLights == null)
        {
            return false;
        }

        foreach (Light2D spotLight in _spotLights)
        {
            if (spotLight == null || !spotLight.isActiveAndEnabled)
            {
                continue;
            }

            Vector2 lightPosition = spotLight.transform.position;
            Vector2 direction = targetPosition - lightPosition;

            float distance = direction.magnitude;

            if (distance <= 0f ||
                distance > spotLight.pointLightOuterRadius)
            {
                continue;
            }

            float angle = Vector2.Angle(
                spotLight.transform.up,
                direction
            );

            if (angle > spotLight.pointLightOuterAngle * 0.5f)
            {
                continue;
            }

            if (!IsBlocked(lightPosition, targetPosition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ライトから対象までの間に遮蔽物があるか判定する。
    /// </summary>
    /// <param name="lightPosition">ライトの位置。</param>
    /// <param name="targetPosition">対象の位置。</param>
    /// <returns>遮蔽物があればtrue。</returns>
    private bool IsBlocked(
        Vector2 lightPosition,
        Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - lightPosition;
        float distance = direction.magnitude;

        if (distance <= 0f)
        {
            return false;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            lightPosition,
            direction.normalized,
            distance,
            _obstacleLayer
        );

        return hit.collider != null;
    }
}
