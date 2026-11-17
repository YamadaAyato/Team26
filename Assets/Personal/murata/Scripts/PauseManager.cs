
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference _pauseAction;

    [Header("UI Panels")]
    [SerializeField] private Image _pauseMenu;
    [SerializeField] private Image _restartConfirm;
    [SerializeField] private Image _titleConfirm;
    [SerializeField] private Image _optionsPanel;
    [SerializeField] private Image _howToPlayPanel;

    [Header("Scene Settings")]
    [SerializeField] private string _titleSceneName = "Title";

    public static bool IsPaused { get; private set; }

    /// <summary>
    /// ゲーム開始時に状態を初期化する
    /// </summary>
    private void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        HideAllPanels();
    }

    /// <summary>
    /// ポーズ入力のイベントを登録する
    /// </summary>
    private void OnEnable()
    {
        if (_pauseAction == null) return;

        _pauseAction.action.performed += OnPausePerformed;
        _pauseAction.action.Enable();
    }

    /// <summary>
    /// ポーズ入力のイベント登録を解除する
    /// </summary>
    private void OnDisable()
    {
        if (_pauseAction == null) return;

        _pauseAction.action.performed -= OnPausePerformed;
        _pauseAction.action.Disable();
    }

    /// <summary>
    /// ESCキーでゲームの一時停止と再開を切り替える
    /// </summary>
    /// <param name="context">入力情報</param>
    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    /// <summary>
    /// ゲームを一時停止する
    /// </summary>
    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        ShowPauseMenu();
    }

    /// <summary>
    /// ゲームを再開する
    /// </summary>
    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        HideAllPanels();
    }

    /// <summary>
    /// ポーズメニューを表示する
    /// </summary>
    public void ShowPauseMenu()
    {
        HideAllPanels();
        _pauseMenu.gameObject.SetActive(true);
    }

    /// <summary>
    /// リスタート確認画面を表示する
    /// </summary>
    public void ShowRestartConfirm()
    {
        HideAllPanels();
        _restartConfirm.gameObject.SetActive(true);
    }

    /// <summary>
    /// リスタートをキャンセルする
    /// </summary>
    public void CancelRestart()
    {
        ShowPauseMenu();
    }

    /// <summary>
    /// 現在のステージを最初から開始する
    /// </summary>
    public void ConfirmRestart()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    /// <summary>
    /// オプション画面を表示する
    /// </summary>
    public void ShowOptions()
    {
        HideAllPanels();
        _optionsPanel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 遊び方画面を表示する
    /// </summary>
    public void ShowHowToPlay()
    {
        HideAllPanels();
        _howToPlayPanel.gameObject.SetActive(true);
    }

    /// <summary>
    /// タイトルに戻る確認画面を表示する
    /// </summary>
    public void ShowTitleConfirm()
    {
        HideAllPanels();
        _titleConfirm.gameObject.SetActive(true);
    }

    /// <summary>
    /// タイトルへの移動をキャンセルする
    /// </summary>
    public void CancelTitle()
    {
        ShowPauseMenu();
    }

    /// <summary>
    /// タイトルシーンに戻る
    /// </summary>
    public void ConfirmTitle()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(_titleSceneName);
    }

    /// <summary>
    /// ポーズメニューに戻る
    /// </summary>
    public void BackToPauseMenu()
    {
        ShowPauseMenu();
    }

    /// <summary>
    /// すべてのUIパネルを非表示にする
    /// </summary>
    private void HideAllPanels()
    {
        _pauseMenu.gameObject.SetActive(false);
        _restartConfirm.gameObject.SetActive(false);
        _titleConfirm.gameObject.SetActive(false);
        _optionsPanel.gameObject.SetActive(false);
        _howToPlayPanel.gameObject.SetActive(false);
    }
}
