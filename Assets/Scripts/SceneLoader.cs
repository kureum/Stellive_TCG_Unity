using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadTitleScene()
    {
        SceneManager.LoadScene("TitleScene");
    }

    public void LoadDeckBuilderScene()
    {
        SceneManager.LoadScene("DeckBuilderScene");
    }

    public void LoadBattleScene()
    {
        SceneManager.LoadScene("BattleScene");
    }

    public void LoadBattleLobbyScene()
    {
        SceneManager.LoadScene("BattleLobbyScene");
    }
}
