
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class OverheadLightController : MonoBehaviour
{
    [Header("実際のライト")]
    public Light2D overheadLight;

    [Header("地面の照射範囲")]
    public Transform lightArea;

    [Header("移動設定")]
    public float moveDistance = 3f;
    public float moveSpeed = 1f;

    [Header("照射範囲のサイズ")]
    public float lightWidth = 3f;
    public float lightHeight = 3f;

    private Vector3 startPosition;

    void Start()
    {
        if (lightArea == null || overheadLight == null)
        {
            Debug.LogError("ライトまたはLightAreaが設定されていません！");
            enabled = false;
            return;
        }

        startPosition = lightArea.position;
        ApplyLightSize();
    }

    void Update()
    {
        // 地面上の照射中心を左右に移動
        float x = Mathf.Sin(Time.time * moveSpeed) * moveDistance;

        Vector3 targetPosition =
            startPosition + new Vector3(x, 0f, 0f);

        // 円と実際のライトを同じ位置に移動
        lightArea.position = targetPosition;
        overheadLight.transform.position = targetPosition;

        ApplyLightSize();
    }

    void ApplyLightSize()
    {
        // 円形・楕円形のサイズ
        lightArea.localScale =
            new Vector3(lightWidth, lightHeight, 1f);

        // 大きい方のサイズをライトの直径として扱う
        float diameter = Mathf.Max(lightWidth, lightHeight);

        // 直径からRadiusを計算
        overheadLight.pointLightOuterRadius = diameter;
    }
}
