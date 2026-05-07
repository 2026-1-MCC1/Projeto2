using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Velocidade de deslocamento do jogador.
    public float speed = 3f;
    // Define se o personagem pode andar no momento.
    private bool movable = true;
    // Permite selecionar o console de cameras no Inspector.
    public CameraConsole console;

    private void Awake()
    {
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

    void Update()
    {
        // Pausa ou libera o movimento quando o console de cameras abre/fecha.
        if (console != null && Input.GetKeyDown(console.OpenCameras))
        {
            movable = !movable;
        }

        // Move o jogador apenas quando a variavel permite.
        if(movable == true)
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            // Usa o eixo local do jogador para andar conforme sua rotacao atual.
            Vector3 movement = new Vector3(moveX, 0f, moveZ);

            transform.Translate(movement * speed * Time.deltaTime);
        }
    }
}
