using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     Unityの参照を永続IDで保持し、下書きと保存済み設定を比較する。
    /// </summary>
    [Serializable]
    internal sealed class AudioGroupDraft
    {
        public AudioGroup.Kind Category;
        public AudioGroup.Order Mode;
        public string FixedId = "";
        public string Folder = "";
        public int Limit = 8;
        public float Interval;
        public List<Item> Items = new List<Item>();

        /// <summary>
        ///     グループの設定を読み出し、Assetから独立したコピーを作る。
        /// </summary>
        internal static AudioGroupDraft Read(AudioGroup group)
        {
            using var serialized = new SerializedObject(group);
            var draft = new AudioGroupDraft
            {
                Category = group.Category, Mode = group.Mode, Limit = group.VoiceLimit,
                Interval = group.MinInterval, FixedId = Id(group.FixedClip),
                Folder = serialized.FindProperty("_folderGuid").stringValue
            };
            for (int i = 0; i < group.Count; i++)
            {
                draft.Items.Add(new Item { Id = Id(group.GetClip(i)), Gain = group.GetGain(i) });
            }

            return draft;
        }

        /// <summary>
        ///     安定した参照IDを取得する。Unityを終了するまでのインスタンス番号には依存しない。
        /// </summary>
        internal static string Id(AudioClip clip) =>
            clip == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(clip).ToString();

        /// <summary>
        ///     移動・改名後も同じ音源を引く。
        /// </summary>
        internal static AudioClip Clip(string id) =>
            GlobalObjectId.TryParse(id, out GlobalObjectId parsed) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as AudioClip : null;

        /// <summary>
        ///     重複・欠損・範囲外値・固定採用の不整合を保存前に検出する。
        /// </summary>
        internal string Validate()
        {
            if (!Enum.IsDefined(typeof(AudioGroup.Kind), Category) || !Enum.IsDefined(typeof(AudioGroup.Order), Mode)) return "種類・方式が不正です。";
            if (Limit < 1 || !float.IsFinite(Interval) || Interval < 0f) return "SEの上限・間隔を確認してください。";
            if (!string.IsNullOrEmpty(Folder) && !AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(Folder)))
                return "登録元フォルダーの参照が切れています。再指定または解除してください。";
            var seen = new HashSet<string>();
            foreach (Item item in Items)
            {
                if (Clip(item.Id) == null || !seen.Add(item.Id)) return "参照切れ、または重複する候補があります。";
                if (!float.IsFinite(item.Gain) || item.Gain < 0f || item.Gain > 10f) return "基準音量は0〜1000%です。";
            }

            if (FixedId != "" && !seen.Contains(FixedId)) return "採用音源が候補にありません。";
            if (Mode == AudioGroup.Order.Fixed && Items.Count > 0 && FixedId == "") return "固定方式の採用音源を指定してください。";
            return "";
        }

        /// <summary>
        ///     専用のグループにだけ下書きを適用する。ファイル保存は呼び出し側が行う。
        /// </summary>
        internal void Apply(AudioGroup group)
        {
            using var serialized = new SerializedObject(group);
            serialized.FindProperty("_category").enumValueIndex = (int)Category;
            serialized.FindProperty("_mode").enumValueIndex = (int)Mode;
            serialized.FindProperty("_fixedClip").objectReferenceValue = Clip(FixedId);
            serialized.FindProperty("_folderGuid").stringValue = Folder;
            serialized.FindProperty("_voiceLimit").intValue = Limit;
            serialized.FindProperty("_minInterval").floatValue = Interval;
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = Items.Count;
            for (int i = 0; i < Items.Count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_clip").objectReferenceValue = Clip(Items[i].Id);
                entry.FindPropertyRelative("_gain").floatValue = Items[i].Gain;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        ///     確認画面に表示する変更項目を作る。
        /// </summary>
        internal IEnumerable<string> Changes(AudioGroupDraft before)
        {
            if (Category != before.Category) yield return $"種類　{before.Category} → {Category}";
            if (Mode != before.Mode) yield return $"再生方式　{OrderName(before.Mode)} → {OrderName(Mode)}";
            if (FixedId != before.FixedId) yield return $"採用　{NameOf(before.FixedId)} → {NameOf(FixedId)}";
            if (Folder != before.Folder) yield return $"登録元　{AssetDatabase.GUIDToAssetPath(before.Folder)} → {AssetDatabase.GUIDToAssetPath(Folder)}";
            if (Limit != before.Limit) yield return $"同時数　{before.Limit} → {Limit}";
            if (Interval != before.Interval) yield return $"最短間隔　{before.Interval} → {Interval} 秒";
            foreach (Item item in Items)
            {
                Item old = before.Items.Find(x => x.Id == item.Id);
                if (old == null) yield return $"追加　{NameOf(item.Id)}　{item.Gain * 100:G5}%";
                else if (old.Gain != item.Gain) yield return $"{NameOf(item.Id)}　{old.Gain * 100:G5}% → {item.Gain * 100:G5}%";
            }

            foreach (Item old in before.Items)
            {
                if (!Items.Exists(x => x.Id == old.Id)) yield return $"解除　{NameOf(old.Id)}";
            }

            if (!Items.Select(x => x.Id).SequenceEqual(before.Items.Select(x => x.Id)))
                yield return "登録順　" + string.Join(" → ", Items.Select(x => NameOf(x.Id)));
        }

        /// <summary>
        ///     未知の選曲方式も確認画面で識別できる名前にする。
        /// </summary>
        internal static string OrderName(AudioGroup.Order mode) => mode switch
        {
            AudioGroup.Order.Fixed => "固定",
            AudioGroup.Order.Random => "ランダム",
            AudioGroup.Order.Sequential => "順番",
            _ => "不明"
        };

        /// <summary>
        ///     参照の現在の表示名を取得する。
        /// </summary>
        internal static string NameOf(string id) => Clip(id)?.name ?? (id == "" ? "未設定" : "参照切れ");

        /// <summary>
        ///     候補一件の参照と基準ゲインを保持する。
        /// </summary>
        [Serializable]
        internal sealed class Item
        {
            public string Id;
            public float Gain = 1f;
        }
    }
}
