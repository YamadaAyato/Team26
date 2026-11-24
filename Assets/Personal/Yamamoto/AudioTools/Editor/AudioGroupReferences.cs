using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary> グループを使用するAssetと編集中のSceneを調べ、削除・種別変更を保護する。 </summary>
    internal static class AudioGroupReferences
    {
        internal const string SCOPE = "Unityに登録されたAssetの直接参照を確認します。文字列による読込や外部データは確認できません。";

        /// <summary> 参照を含む集合だけを分割し、対象自身を除く直接参照元を特定する。 </summary>
        internal static string[] Find(AudioGroupDocument doc)
        {
            if (doc.New) return Array.Empty<string>();
            var found = new SortedSet<string>(StringComparer.Ordinal);
            var paths = AssetDatabase.GetAllAssetPaths().Where(p => p != doc.Path &&
                (p.StartsWith("Assets/", StringComparison.Ordinal) || p.StartsWith("Packages/", StringComparison.Ordinal)) &&
                !AssetDatabase.IsValidFolder(p)).ToArray();
            // 初回は依存情報の準備に時間がかかるため、分割して進行状況を表示する。
            const int BATCH = 512;
            bool showing = false;
            double start = EditorApplication.timeSinceStartup;
            try
            {
                for (int i = 0; i < paths.Length; i += BATCH)
                {
                    if (!Application.isBatchMode && EditorApplication.timeSinceStartup - start >= 0.5)
                    {
                        showing = true;
                        if (EditorUtility.DisplayCancelableProgressBar("グループの参照元を確認中",
                            $"{i:N0} / {paths.Length:N0} Asset", (float)i / paths.Length))
                            throw new OperationCanceledException("参照の確認を取り消しました。");
                    }

                    FindIn(paths.Skip(i).Take(BATCH).ToArray(), doc.Path, found);
                }
            }
            finally { if (showing) EditorUtility.ClearProgressBar(); }

            AudioGroup group = AssetDatabase.LoadAssetAtPath<AudioGroup>(doc.Path);
            if (group == null) throw new InvalidOperationException("参照先のグループを読み込めません。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                CheckScene(SceneManager.GetSceneAt(i), group, found);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) CheckScene(stage.scene, group, found);
            foreach (var asset in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (asset == group || !EditorUtility.IsPersistent(asset) || !EditorUtility.IsDirty(asset)) continue;
                if (EditorUtility.CollectDependencies(new UnityEngine.Object[] { asset }).Contains(group))
                    found.Add(AssetDatabase.GetAssetPath(asset));
            }

            return found.ToArray();
        }

        /// <summary> 未保存のScene・Prefabで新しく追加された参照も確認する。 </summary>
        private static void CheckScene(Scene scene, AudioGroup group, ISet<string> found)
        {
            if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty) return;
            if (EditorUtility.CollectDependencies(scene.GetRootGameObjects()).Contains(group))
                found.Add(string.IsNullOrEmpty(scene.path) ? "未保存のScene" : scene.path);
        }

        /// <summary> 複数Assetの依存をまとめて問い合わせ、参照元のパスを収集する。 </summary>
        private static void FindIn(string[] paths, string target, ISet<string> found)
        {
            if (paths.Length == 0 || !AssetDatabase.GetDependencies(paths, false).Contains(target)) return;
            if (paths.Length == 1) { found.Add(paths[0]); return; }
            int half = paths.Length / 2;
            FindIn(paths.Take(half).ToArray(), target, found);
            FindIn(paths.Skip(half).ToArray(), target, found);
        }

        /// <summary> 構成を変更するときだけ、試聴停止・参照元の制約を適用する。 </summary>
        internal static string Issue(bool delete, string[] sources)
        {
            GroupAudioPlayer player = AudioToolsTransport.Player();
            if (player != null && (player.CurrentMusic != null || player.OutgoingMusic != null || player.PlayingEffects > 0))
                return "削除・種別変更の前に、再生中のBGM・SEを停止してください。";
            if (delete && sources.Length > 0) return "参照元があるため削除できません。";
            if (!delete && sources.Any(p => !p.StartsWith(AudioGroupDocument.ROOT + "/", StringComparison.Ordinal)))
                return "ツール外の参照元があります。種別変更には参照元の担当者との確認が必要です。";
            return "";
        }

        /// <summary> 保存直前にも新しい参照を検出し、下書きを保持する。 </summary>
        internal static string BlockReason(AudioGroupDocument doc)
        {
            string playback = Issue(doc.DeletePending, Array.Empty<string>());
            if (playback != "") return playback;
            string[] sources = Find(doc);
            string issue = Issue(doc.DeletePending, sources);
            return issue == "" || sources.Length == 0 ? issue : issue + "\n" + string.Join("\n", sources);
        }
    }
}
