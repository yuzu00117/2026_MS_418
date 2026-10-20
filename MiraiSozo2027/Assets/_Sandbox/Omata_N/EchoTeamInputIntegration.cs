using System.Collections.Generic;
using Echo.Echolocation;
using Echo.Echolocation.Samples;
using UnityEngine;
using UnityEngine.SceneManagement;

// Assembly-CSharpに置き、既存のチーム操作とasmdef内のエコーを接続する。
// InputActionの生成・有効化や、GameManagerの入力設定変更は行わない。
[DefaultExecutionOrder(-32000)]
public sealed class EchoTeamInputIntegration : MonoBehaviour
{
    private readonly List<Behaviour> _suspended = new List<Behaviour>();
    private bool _teamWasActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Domain/Scene Reload無効でも監視を重複させない。
        foreach (var existing in FindObjectsByType<EchoTeamInputIntegration>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing)
                return;
        var host = new GameObject("Echo Team Input Integration");
        DontDestroyOnLoad(host);
        host.AddComponent<EchoTeamInputIntegration>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerController.OnPlayerSpawned += OnPlayerSpawned;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerController.OnPlayerSpawned -= OnPlayerSpawned;
        Restore();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();
    private void OnPlayerSpawned(PlayerController player) => Refresh();
    private void Update()
    {
        var player = PlayerController.Instance;
        bool teamActive = player && player.isActiveAndEnabled;
        if (teamActive != _teamWasActive)
            Refresh();
    }

    private void Refresh()
    {
        var player = PlayerController.Instance;
        _teamWasActive = player && player.isActiveAndEnabled;
        if (!_teamWasActive)
        {
            Restore();
            return;
        }

        // シーン追加・プレイヤー生成時にも停止し、毎フレームの全体検索は避ける。
        foreach (var input in FindObjectsByType<EchoPrototypeInput>(FindObjectsSortMode.None))
            Suspend(input);
        var cursorLock = Cursor.lockState;
        var cursorVisible = Cursor.visible;
        foreach (var demo in FindObjectsByType<EchoDemoPlayer>(FindObjectsSortMode.None))
            Suspend(demo);
        // デモのOnDisableによるカーソル解放でチームのポーズ状態を上書きしない。
        Cursor.lockState = cursorLock;
        Cursor.visible = cursorVisible;
    }

    private void Suspend(Behaviour component)
    {
        if (!component.enabled)
            return;
        _suspended.Add(component);
        component.enabled = false;
    }

    private void Restore()
    {
        foreach (var component in _suspended)
            if (component)
                component.enabled = true;
        _suspended.Clear();
    }
}
