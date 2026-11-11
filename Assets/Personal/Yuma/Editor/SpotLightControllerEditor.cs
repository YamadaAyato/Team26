
using UnityEditor;
using UnityEngine;

/// <summary>
///     SceneビューからSpotLightControllerの照射距離と角度を調整する。
/// </summary>
[CustomEditor(typeof(SpotLightController))]
public class SpotLightControllerEditor : Editor
{
    private SerializedProperty _lightDistance;
    private SerializedProperty _lightAngle;

    /// <summary>
    ///     調整対象の設定値を取得する。
    /// </summary>
    private void OnEnable()
    {
        _lightDistance = serializedObject.FindProperty("_lightDistance");
        _lightAngle = serializedObject.FindProperty("_lightAngle");
    }

    /// <summary>
    ///     Sceneビューに照射範囲の操作ハンドルを表示する。
    /// </summary>
    private void OnSceneGUI()
    {
        SpotLightController controller = (SpotLightController)target;
        Transform lightTransform = controller.transform;

        serializedObject.Update();

        Vector3 origin = lightTransform.position;
        Vector3 forward = lightTransform.up;

        float distance = _lightDistance.floatValue;
        float angle = _lightAngle.floatValue;

        float halfAngle = angle * 0.5f;

        Vector3 leftDirection =
            Quaternion.AngleAxis(-halfAngle, Vector3.forward) * forward;

        Vector3 rightDirection =
            Quaternion.AngleAxis(halfAngle, Vector3.forward) * forward;

        Handles.color = Color.yellow;

        Handles.DrawWireArc(
            origin,
            Vector3.forward,
            leftDirection,
            angle,
            distance);

        Handles.DrawLine(origin, origin + leftDirection * distance);
        Handles.DrawLine(origin, origin + rightDirection * distance);

        EditorGUI.BeginChangeCheck();

        Vector3 distancePosition = origin + forward * distance;

        Vector3 newDistancePosition = Handles.Slider(
            distancePosition,
            forward,
            HandleUtility.GetHandleSize(distancePosition) * 0.12f,
            Handles.ConeHandleCap,
            0f);

        if (EditorGUI.EndChangeCheck())
        {
            float newDistance =
                Vector3.Dot(newDistancePosition - origin, forward);

            _lightDistance.floatValue =
                Mathf.Clamp(newDistance, 0.1f, 20f);
        }

        DrawAngleHandle(origin, forward, -1f, distance);
        DrawAngleHandle(origin, forward, 1f, distance);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    ///     照射角度を変更するハンドルを表示する。
    /// </summary>
    private void DrawAngleHandle(
        Vector3 origin,
        Vector3 forward,
        float direction,
        float distance)
    {
        float halfAngle = _lightAngle.floatValue * 0.5f;

        Vector3 handleDirection =
            Quaternion.AngleAxis(
                halfAngle * direction,
                Vector3.forward) * forward;

        Vector3 handlePosition =
            origin + handleDirection * distance;

        Handles.color = Color.cyan;

        EditorGUI.BeginChangeCheck();

        Vector3 newHandlePosition = Handles.FreeMoveHandle(
            handlePosition,
            HandleUtility.GetHandleSize(handlePosition) * 0.1f,
            Vector3.zero,
            Handles.DotHandleCap);

        if (EditorGUI.EndChangeCheck())
        {
            Vector3 newDirection =
                (newHandlePosition - origin).normalized;

            float newHalfAngle =
                Mathf.Abs(Vector3.SignedAngle(
                    forward,
                    newDirection,
                    Vector3.forward));

            _lightAngle.floatValue =
                Mathf.Clamp(newHalfAngle * 2f, 1f, 170f);
        }
    }
}
