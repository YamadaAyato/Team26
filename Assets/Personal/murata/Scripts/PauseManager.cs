
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference pauseAction;

    [Header("UI Panels")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject restartConfirm;
    [SerializeField] private GameObject titleConfirm;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject howToPlayPanel;

    [Header("Scene Settings")]
    [SerializeField] private string titleSceneName = "Title";

    private bool isPaused = false;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        HideAllPanels();
    }

    private void OnEnable()
    {
        if (pauseAction != null)
        {
            pauseAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (pauseAction != null)
        {
            pauseAction.action.Disable();
        }
    }

    private void Update()
    {
        // ESCキーでポーズ切り替え
        if (pauseAction != null &&
            pauseAction.action.WasPressedThisFrame())
        {
            if (isPaused)
            {
                ShowPauseMenu();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // ゲーム一時停止
    public void PauseGame()
    {
        isPaused = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        ShowPauseMenu();
    }

    // ゲーム再開
    public void ResumeGame()
    {
        isPaused = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        HideAllPanels();
    }

    // ポーズメニュー表示
    public void ShowPauseMenu()
    {
        HideAllPanels();
        pauseMenu.SetActive(true);
    }

    // リスタート確認画面
    public void ShowRestartConfirm()
    {
        HideAllPanels();
        restartConfirm.SetActive(true);
    }

    // リスタートをキャンセル
    public void CancelRestart()
    {
        ShowPauseMenu();
    }

    // リスタート実行
    public void ConfirmRestart()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    // オプション画面
    public void ShowOptions()
    {
        HideAllPanels();
        optionsPanel.SetActive(true);
    }

    // 遊び方画面
    public void ShowHowToPlay()
    {
        HideAllPanels();
        howToPlayPanel.SetActive(true);
    }

    // タイトルに戻る確認画面
    public void ShowTitleConfirm()
    {
        HideAllPanels();
        titleConfirm.SetActive(true);
    }

    // タイトルに戻るのをキャンセル
    public void CancelTitle()
    {
        ShowPauseMenu();
    }

    // タイトルに戻る
    public void ConfirmTitle()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(titleSceneName);
    }

    // 各画面からポーズメニューに戻る
    public void BackToPauseMenu()
    {
        ShowPauseMenu();
    }

    // 全パネルを非表示
    private void HideAllPanels()
    {
        pauseMenu.SetActive(false);
        restartConfirm.SetActive(false);
        titleConfirm.SetActive(false);
        optionsPanel.SetActive(false);
        howToPlayPanel.SetActive(false);
    }
}
