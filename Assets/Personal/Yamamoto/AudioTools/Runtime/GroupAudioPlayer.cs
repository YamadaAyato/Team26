using UnityEngine;
using UnityEngine.Audio;

namespace Yamamoto.AudioTools
{
    /// <summary>
    ///     Inspectorで割り当てた専用Mixerへグループ再生を要求する。
    ///     Scene上の参照口が交代しても、演奏中の出力は一つだけ維持する。
    /// </summary>
    public sealed class GroupAudioPlayer : MonoBehaviour
    {
        /// <summary> 再生中またはフェードイン待ちの曲を取得する。 </summary>
        public AudioClip CurrentMusic => GroupPlayback.Current?.CurrentClip;
        /// <summary> フェードアウト中の曲を取得する。 </summary>
        public AudioClip OutgoingMusic => GroupPlayback.Current?.OutgoingClip;
        /// <summary> 現在再生中のSE数を取得する。 </summary>
        public int PlayingEffects => GroupPlayback.Current?.EffectCount ?? 0;
        /// <summary> 二曲を重ねる切り替え中かどうかを取得する。 </summary>
        public bool IsCrossfading => GroupPlayback.Current?.IsCrossfading == true;

        [SerializeField, Tooltip("固定で+20 dBに設定したツール専用の出力先。")] private AudioMixerGroup _output;

        /// <summary>
        ///     同じグループの演奏を維持し、他のグループは即時に切り替える。
        /// </summary>
        public bool PlayBgm(AudioGroup group, bool loop = true) =>
            Change(group, loop, 0f, GroupPlayback.Transition.Immediate);

        /// <summary>
        ///     指定グループへ即時に切り替える。
        /// </summary>
        public bool SwitchBgm(AudioGroup group, bool loop = true) => PlayBgm(group, loop);

        /// <summary>
        ///     指定秒数の前半で旧曲を消し、後半で新曲を立ち上げる。
        /// </summary>
        public bool FadeSwitchBgm(AudioGroup group, float duration, bool loop = true) =>
            Change(group, loop, duration, GroupPlayback.Transition.Sequential);

        /// <summary>
        ///     指定秒数で二つの曲を重ねながら切り替える。
        /// </summary>
        public bool CrossfadeBgm(AudioGroup group, float duration, bool loop = true) =>
            Change(group, loop, duration, GroupPlayback.Transition.Cross);

        /// <summary>
        ///     進行中の切り替えを含むすべてのBGMを停止する。
        /// </summary>
        public void StopBgm() => GroupPlayback.Current?.StopMusic();

        /// <summary>
        ///     同時数と間隔の制限を満たす場合だけSEを一回再生する。
        /// </summary>
        public bool PlaySe(AudioGroup group) => PlaySe(group, -1);

        /// <summary>
        ///     指定グループのSEを停止する。省略時はすべてのSEを停止する。
        /// </summary>
        public void StopSe(AudioGroup group = null) => GroupPlayback.Current?.StopEffects(group);

        /// <summary>
        ///     試聴する候補を指定して、採用設定を変更せずに再生する。
        /// </summary>
        public bool Audition(AudioGroup group, int index, bool loop)
        {
            if (group == null || index < 0 || index >= group.Count || group.GetClip(index) == null) return false;
            if (group.Category == AudioGroup.Kind.Se) return PlaySe(group, index);
            return Change(group, loop, 0f, GroupPlayback.Transition.Immediate, index);
        }

        /// <summary>
        ///     再生位置を維持して、指定された試聴音の基準ゲインを更新する。
        /// </summary>
        public void SetAuditionGain(AudioGroup group, AudioClip clip, float gain) =>
            GroupPlayback.Current?.SetGain(group, clip, gain);

        /// <summary>
        ///     全試聴音に掛けるモニター倍率を設定する。
        /// </summary>
        public void SetMonitorLevel(float level)
        {
            if (_output != null && Application.isPlaying) GroupPlayback.GetOrCreate(_output).SetMonitor(level);
        }

        /// <summary>
        ///     指定候補またはグループの規則で効果音を要求する。
        /// </summary>
        private bool PlaySe(AudioGroup group, int index)
        {
            return Application.isPlaying && _output != null && group != null && group.Category == AudioGroup.Kind.Se &&
                GroupPlayback.GetOrCreate(_output).PlayEffect(group, index);
        }

        /// <summary>
        ///     用途と参照を検査してからBGMの切替を委譲する。
        /// </summary>
        private bool Change(AudioGroup group, bool loop, float duration, GroupPlayback.Transition transition, int index = -1)
        {
            return Application.isPlaying && _output != null && group != null && group.Category == AudioGroup.Kind.Bgm &&
                GroupPlayback.GetOrCreate(_output).ChangeMusic(group, loop, duration, transition, index);
        }
    }
}
