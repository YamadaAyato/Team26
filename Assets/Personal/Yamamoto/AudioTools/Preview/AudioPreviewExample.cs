using UnityEngine;

namespace Yamamoto.AudioTools
{
    /// <summary>
    ///     専用SceneでInspectorの参照とゲーム側からの呼び出し方を実演する。
    /// </summary>
    public sealed class AudioPreviewExample : MonoBehaviour
    {
        [Header("再生部品"), SerializeField] private GroupAudioPlayer _audioPlayer;
        [Header("吸い込みSE"), SerializeField] private AudioGroup _inhaleSe;
        [Header("BGM・足音のサンプル")]
        [SerializeField] private AudioGroup _exploreBgm;
        [SerializeField] private AudioGroup _chaseBgm;
        [SerializeField] private AudioGroup _footstepSe;

        /// <summary>
        ///     探索BGMを保存された設定で継続再生する。
        /// </summary>
        [ContextMenu("探索BGMを再生")]
        public void PlayExplore() => _audioPlayer.PlayBgm(_exploreBgm, loop: true);

        /// <summary>
        ///     登録した効果音を一回再生する。
        /// </summary>
        [ContextMenu("足音SEを再生")]
        public void PlayFootstep() => _audioPlayer.PlaySe(_footstepSe);

        /// <summary>
        ///     トリセツの呼び出し例を、Inspectorに割り当てた保存済み設定で実行する。
        /// </summary>
        [ContextMenu("吸い込みSEを1回鳴らす")]
        public void PlayInhale()
        {
            _audioPlayer.PlaySe(_inhaleSe);
        }

        /// <summary>
        ///     追跡BGMへ即時に切り替える。
        /// </summary>
        [ContextMenu("追跡BGMに即時切替")]
        public void SwitchToChase() => _audioPlayer.SwitchBgm(_chaseBgm);

        /// <summary>
        ///     追跡BGMへ順次フェードで切り替える。
        /// </summary>
        [ContextMenu("追跡BGMに順次フェード")]
        public void FadeToChase() => _audioPlayer.FadeSwitchBgm(_chaseBgm, duration: 2f);

        /// <summary>
        ///     追跡BGMへクロスフェードする。
        /// </summary>
        [ContextMenu("追跡BGMにクロスフェード")]
        public void CrossfadeToChase() => _audioPlayer.CrossfadeBgm(_chaseBgm, duration: 2f);

        /// <summary>
        ///     専用SceneのBGMとSEを停止する。
        /// </summary>
        [ContextMenu("全停止")]
        public void StopAudio()
        {
            _audioPlayer.StopBgm();
            _audioPlayer.StopSe();
        }
    }
}
