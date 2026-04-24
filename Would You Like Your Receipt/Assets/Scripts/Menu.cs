using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("Game"); // nome da sua cena do jogo
    }

    public void QuitGame()
    {
        Debug.Log("Saiu do jogo"); // aparece no editor
        Application.Quit(); // funciona no build
    }
}
