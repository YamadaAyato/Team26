using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary> 自作音と使い捨ての参照元で、グループ管理の保存境界を検証する。 </summary>
    internal static partial class AudioToolsVerification
    {
        /// <summary> 改名・種別変更・複製・削除と、保存時に増えた参照を実際のAssetで検証する。 </summary>
        private static void VerifyGroupEditing()
        {
            string name = "Verification-" + Guid.NewGuid().ToString("N");
            string path = AudioGroupDocument.DATA + name + ".asset";
            var ids = new List<string>();
            var window = EditorWindow.GetWindow<AudioToolsWindow>();
            string before = SessionState.GetString(AudioToolsWindow.SESSION, "");
            try
            {
                var draft = MakeTestDraft(AudioGroup.Kind.Se, true, 0.73f);
                draft.Limit = 3;
                draft.Interval = 0.125f;
                var initial = new AudioGroupDocument { Path = path, New = true, Draft = draft, Baseline = new AudioGroupDraft() }.Save();
                ids.Add(initial.AssetId);
                AudioToolsWindow.OpenGroup(path);
                AudioGroupDocument doc = window.SelectedDocument;
                string prefab = AudioGroupDocument.DATA + name + ".prefab";
                CreateReferencePrefab(prefab, path);
                ids.Add(AssetDatabase.AssetPathToGUID(prefab));
                window.BeginGroupAction(doc, AudioToolsWindow.GroupAction.Delete);
                Check(!window.ApplyGroupAction(doc, "") && !doc.DeletePending, "referenced prefab blocks deletion");
                window.BeginGroupAction(doc, AudioToolsWindow.GroupAction.Rename);
                Check(window.ApplyGroupAction(doc, name + "-Renamed") && File.Exists(path) && !File.Exists(doc.TargetPath), "rename staged");
                var occupied = new AudioGroupDocument { Path = doc.TargetPath, New = true,
                    Draft = MakeTestDraft(AudioGroup.Kind.Bgm), Baseline = new AudioGroupDraft() }.Save();
                ids.Add(occupied.AssetId);
                Check(window.SaveReviewed(new[] { doc }) == 0 && doc.PendingName == name + "-Renamed" &&
                    AudioGroupDocument.Load(path).AssetId == initial.AssetId && AudioGroupDocument.Load(occupied.Path).AssetId == occupied.AssetId,
                    "late rename collision preserves original asset, destination and pending name");
                RemoveFixture(occupied.AssetId);
                Check(window.SaveReviewed(new[] { doc }) == 1, "rename saved");
                doc = window.SelectedDocument;
                using (var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponent<AudioPreviewExample>()))
                {
                    var target = serialized.FindProperty("_exploreBgm").objectReferenceValue;
                    Check(doc.AssetId == initial.AssetId && AssetDatabase.GetAssetPath(target) == doc.Path && !File.Exists(path),
                        "rename preserves GUID and Inspector reference");
                }

                foreach (var kind in new[] { AudioGroup.Kind.Bgm, AudioGroup.Kind.Se })
                {
                    window.BeginGroupAction(doc, AudioToolsWindow.GroupAction.Kind);
                    Check(window.ApplyGroupAction(doc, "") && window.SaveReviewed(new[] { doc }) == 1, "type change saved " + kind);
                    doc = window.SelectedDocument;
                    draft.Category = kind;
                    Check(doc.AssetId == initial.AssetId && JsonUtility.ToJson(doc.Draft) == JsonUtility.ToJson(draft),
                        "both kinds preserve source order, gains, mode, fixed clip and dormant SE limits");
                }

                window.BeginGroupAction(doc, AudioToolsWindow.GroupAction.Copy);
                Check(!window.ApplyGroupAction(doc, Path.GetFileNameWithoutExtension(doc.Path)) &&
                    window.ApplyGroupAction(doc, name + "-Copy"), "collision blocked and independent copy staged");
                AudioGroupDocument copy = window.SelectedDocument;
                Check(copy.New && !File.Exists(copy.Path) && JsonUtility.ToJson(copy.Draft) == JsonUtility.ToJson(doc.Draft), "copy matches settings");
                copy.Draft.Items[0].Gain = 0.42f;
                Check(doc.Draft.Items[0].Gain == 0.73f && window.SaveReviewed(new[] { copy }) == 1, "copy gain independent");
                copy = window.SelectedDocument;
                ids.Add(copy.AssetId);
                Check(copy.AssetId != doc.AssetId, "copy has separate GUID");
                window.BeginGroupAction(copy, AudioToolsWindow.GroupAction.Delete);
                Check(window.ApplyGroupAction(copy, "") && copy.DeletePending && File.Exists(copy.Path), "delete pending only");
                copy.DeletePending = false;
                Check(!copy.Dirty && File.Exists(copy.Path), "delete reservation cancelled");
                window.BeginGroupAction(copy, AudioToolsWindow.GroupAction.Delete);
                Check(window.ApplyGroupAction(copy, ""), "delete staged again");
                string late = AudioGroupDocument.DATA + name + "-Late.prefab";
                CreateReferencePrefab(late, copy.Path);
                string lateId = AssetDatabase.AssetPathToGUID(late);
                ids.Add(lateId);
                Check(window.SaveReviewed(new[] { copy }) == 0 && copy.DeletePending && File.Exists(copy.Path), "late dependency blocks save and retains draft");
                RemoveFixture(lateId);
                Check(window.SaveReviewed(new[] { copy }) == 1 && !File.Exists(copy.Path) &&
                    File.Exists(doc.Path) && TestTone("Tone440") != null && TestTone("Tone220") != null, "deletion leaves original group and both clips");
            }
            finally
            {
                for (int i = ids.Count - 1; i >= 0; i--) RemoveFixture(ids[i]);
                window.RestoreSession(before);
            }
        }

        /// <summary> 編集中のSceneに触れず、Inspectorからグループを使用する検証用Prefabを作る。 </summary>
        private static void CreateReferencePrefab(string destination, string path)
        {
            if (AudioGroupDocument.Exists(destination)) throw new IOException("検証用の保存先が使用中です。");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var template = AssetDatabase.LoadAssetAtPath<GameObject>(AudioGroupDocument.ROOT + "/Runtime/GroupAudioPlayer.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(template, preview);
                var example = root.AddComponent<AudioPreviewExample>();
                using var serialized = new SerializedObject(example);
                serialized.FindProperty("_exploreBgm").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioGroup>(path);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, destination) == null) throw new IOException("検証用Prefabの作成に失敗しました。");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        /// <summary> この試験が生成したGUIDを照合し、現在のパスにある検証Assetだけを片付ける。 </summary>
        private static void RemoveFixture(string id)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            if (path.StartsWith(AudioGroupDocument.DATA + "Verification-", StringComparison.Ordinal) &&
                AssetDatabase.AssetPathToGUID(path) == id) AssetDatabase.DeleteAsset(path);
        }
    }
}
