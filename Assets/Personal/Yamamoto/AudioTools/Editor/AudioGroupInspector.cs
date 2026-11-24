using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     Inspectorでは保存内容の閲覧に限定し、変更は下書き画面へ案内する。
    /// </summary>
    [CustomEditor(typeof(AudioGroup))]
    internal sealed class AudioGroupInspector : UnityEditor.Editor
    {
        /// <summary>
        ///     保存済みのグループ設定を表示する。
        /// </summary>
        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(true)) DrawDefaultInspector();
            if (GUILayout.Button("音声ツールで開く")) AudioToolsWindow.OpenGroup(AssetDatabase.GetAssetPath(target));
        }
    }
}
