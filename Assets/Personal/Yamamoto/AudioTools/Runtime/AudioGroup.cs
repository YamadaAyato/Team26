using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yamamoto.AudioTools
{
    /// <summary>
    ///     用途ごとの候補音源、基準ゲイン、選曲規則を保持する。
    /// </summary>
    public sealed class AudioGroup : ScriptableObject
    {
        /// <summary> 音の用途を取得する。 </summary>
        public Kind Category => _category;
        /// <summary> 選曲方式を取得する。 </summary>
        public Order Mode => _mode;
        /// <summary> 固定方式で使う音源を取得する。 </summary>
        public AudioClip FixedClip => _fixedClip;
        /// <summary> 登録順の候補数を取得する。 </summary>
        public int Count => _entries.Count;
        /// <summary> SEの同時再生上限を取得する。 </summary>
        public int VoiceLimit => _voiceLimit;
        /// <summary> SEを再度受理するまでの秒数を取得する。 </summary>
        public float MinInterval => _minInterval;

        public enum Kind { Bgm, Se }
        public enum Order { Fixed, Random, Sequential }

        [SerializeField, Tooltip("グループの用途。")] private Kind _category;
        [SerializeField, Tooltip("候補を選ぶ方法。")] private Order _mode;
        [SerializeField, Tooltip("固定方式で採用する音源。")] private AudioClip _fixedClip;
        [SerializeField, Tooltip("登録順の音源と個別ゲイン。")] private List<Entry> _entries = new List<Entry>();
        [SerializeField, Min(1), Tooltip("同時に鳴らせるSE数。")] private int _voiceLimit = 8;
        [SerializeField, Min(0f), Tooltip("SEの最短再生間隔（秒）。")] private float _minInterval;
        [SerializeField, HideInInspector] private string _folderGuid;

        /// <summary>
        ///     登録位置に対応する音源を取得する。
        /// </summary>
        public AudioClip GetClip(int index) => _entries[index].Clip;

        /// <summary>
        ///     登録位置に対応する線形ゲインを取得する。
        /// </summary>
        public float GetGain(int index) => _entries[index].Gain;

        /// <summary>
        ///     有効な候補から次に使う登録位置を選ぶ。
        /// </summary>
        internal int SelectNext(int previous)
        {
            if (Mode == Order.Sequential)
            {
                for (int offset = 1; offset <= Count; offset++)
                {
                    int next = (previous + offset) % Count;
                    if (GetClip(next) != null) return next;
                }

                return -1;
            }

            int choice = -1;
            int eligible = 0;
            for (int i = 0; i < Count; i++)
            {
                AudioClip clip = GetClip(i);
                if (clip == null) continue;
                if (Mode == Order.Fixed && clip == FixedClip) return i;
                if (Mode == Order.Random && i != previous && UnityEngine.Random.Range(0, ++eligible) == 0) choice = i;
            }

            if (choice < 0 && Mode == Order.Random && previous >= 0 && previous < Count && GetClip(previous) != null) return previous;
            return choice;
        }

        /// <summary>
        ///     一つのグループ内でのみ有効な音源と音量の組を保持する。
        /// </summary>
        [Serializable]
        private sealed class Entry
        {
            /// <summary> 登録した音源を取得する。 </summary>
            public AudioClip Clip => _clip;
            /// <summary> 元の振幅に掛ける倍率を取得する。 </summary>
            public float Gain => _gain;

            [SerializeField, Tooltip("この用途で鳴らす音源。")] private AudioClip _clip;
            [SerializeField, Range(0f, 10f), Tooltip("元の信号振幅に掛ける倍率。")] private float _gain = 1f;
        }
    }
}
