using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public void PlayGame()
    {
        // Carrega a cena principal do jogo.
        SceneManager.LoadScene("Game");
    }

    public void QuitGame()
    {
        // Mostra feedback no Editor e fecha o jogo no build.
        Debug.Log("Saiu do jogo");
        Application.Quit();
    }
}
