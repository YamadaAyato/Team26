using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     専用Sceneの再生部品に、Assetから独立した試聴用設定を渡す。
    /// </summary>
    internal sealed class AudioToolsTransport
    {
        private readonly Dictionary<string, AudioGroup> _copies = new Dictionary<string, AudioGroup>();
        private static GroupAudioPlayer _player;

        /// <summary>
        ///     現在の専用Sceneから試聴用の参照口を取得する。
        /// </summary>
        internal static GroupAudioPlayer Player()
        {
            if (!EditorApplication.isPlaying) return null;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != AudioToolsWindow.SCENE && scene.path != AudioToolsWindow.NEXT_SCENE) return null;
            if (_player != null && _player.gameObject.scene == scene) return _player;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out _player)) return _player;
            }

            return null;
        }

        /// <summary>
        ///     現在の候補・方式と、AまたはBの個別音量から試聴設定を組み立てる。
        /// </summary>
        internal AudioGroup Refresh(AudioGroupDocument doc, bool listenB, GroupAudioPlayer player)
        {
            if (!_copies.TryGetValue(doc.Path, out AudioGroup copy) || copy == null)
            {
                copy = ScriptableObject.CreateInstance<AudioGroup>();
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.name = System.IO.Path.GetFileNameWithoutExtension(doc.Path);
                _copies[doc.Path] = copy;
            }

            var audition = JsonUtility.FromJson<AudioGroupDraft>(JsonUtility.ToJson(doc.Draft));
            foreach (AudioGroupDraft.Item item in audition.Items)
            {
                if (!listenB) item.Gain = doc.Baseline.Items.Find(x => x.Id == item.Id)?.Gain ?? 1f;
                player.SetAuditionGain(copy, AudioGroupDraft.Clip(item.Id), item.Gain);
            }

            audition.Apply(copy);
            return copy;
        }

        /// <summary> 改名後も同じ再生コピーを保持し、停止済みの削除対象だけを解放する。 </summary>
        internal void ChangePath(string before, string after)
        {
            if (!_copies.TryGetValue(before, out AudioGroup copy)) return;
            _copies.Remove(before);
            if (after == null) { if (copy != null) Object.DestroyImmediate(copy); return; }
            _copies[after] = copy;
            if (copy != null) copy.name = System.IO.Path.GetFileNameWithoutExtension(after);
        }

        /// <summary>
        ///     専用Sceneの音を止め、試聴コピーを解放する。
        /// </summary>
        internal void Clear()
        {
            GroupAudioPlayer player = Player();
            if (player != null) { player.StopBgm(); player.StopSe(); }
            foreach (AudioGroup group in _copies.Values)
            {
                if (group != null) Object.DestroyImmediate(group);
            }

            _copies.Clear();
            _player = null;
        }
    }
}
