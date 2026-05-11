using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    // Abre a cena principal quando o jogador clica em jogar.
    public void PlayGame()
    {
        // Carrega a cena principal do jogo.
        SceneManager.LoadScene("Game");
    }

    // Fecha a aplicacao quando o jogador escolhe sair.
    public void QuitGame()
    {
        // Mostra feedback no Editor e fecha o jogo no build.
        Debug.Log("Saiu do jogo");
        Application.Quit();
    }
}
