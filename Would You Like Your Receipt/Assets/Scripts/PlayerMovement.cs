using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Este script move o jogador no mundo usando WASD/eixos de input.
    // A rotacao vem da camera, e este script cuida apenas do deslocamento.
    // Script principal de locomocao do jogador.
    // Velocidade de deslocamento do jogador.
    public float speed = 3f;
    // Define se o personagem pode andar no momento.
    private bool movable = true;
    // Permite selecionar o console de cameras no Inspector.
    public CameraConsole console;

    private void Awake()
    {
        // Antes do jogo rodar, tenta encontrar o console de cameras.
        // Isso permite pausar o movimento quando o jogador abre as cameras.
        if (console == null)
        {
            // Procura automaticamente o CameraConsole caso o campo nao tenha sido ligado no Inspector.
            console = Object.FindFirstObjectByType<CameraConsole>();
        }

        if (console == null)
        {
            Debug.LogWarning("CameraConsole was not found for PlayerMovement.", this);
        }
    }

    private void Update()
    {
        // Pausa ou libera o movimento quando o console de cameras abre/fecha.
        if (console != null && Input.GetKeyDown(console.OpenCameras))
        {
            movable = !movable;
        }

        // Move o jogador apenas quando a variavel permite.
        if (movable)
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            // Usa o eixo local do jogador para andar conforme sua rotacao atual.
            Vector3 movement = new Vector3(moveX, 0f, moveZ);

            // Move no espaco local para o personagem respeitar sua orientacao atual.
            transform.Translate(movement * speed * Time.deltaTime);
        }
    }
}
