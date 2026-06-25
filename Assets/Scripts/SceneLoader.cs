using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadTitleScene()
    {
        EndOnlineBattleSessionIfNeeded("LoadTitleScene");
        SceneManager.LoadScene("TitleScene");
    }

    public void LoadDeckBuilderScene()
    {
        EndOnlineBattleSessionIfNeeded("LoadDeckBuilderScene");
        SceneManager.LoadScene("DeckBuilderScene");
    }

    public void LoadBattleScene()
    {
        SceneManager.LoadScene("BattleScene");
    }

    public void LoadBattleLobbyScene()
    {
        EndOnlineBattleSessionIfNeeded("LoadBattleLobbyScene");
        SceneManager.LoadScene("BattleLobbyScene");
    }

    private void EndOnlineBattleSessionIfNeeded(string reason)
    {
        OnlineBattleSession.EndActiveSessionBeforeSceneChange(reason);
    }
}
