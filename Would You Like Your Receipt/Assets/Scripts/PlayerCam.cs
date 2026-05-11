using UnityEngine;

public class PlayerCam : MonoBehaviour
{
    // Script responsavel por girar a camera em primeira pessoa.
    // Corpo do jogador que gira junto com a camera no eixo horizontal.
    public Transform player;
    // Sensibilidade do mouse para controlar a camera.
    public float mouseSens = 2f;
    // Rotacao vertical acumulada da camera.
    float camRotationY = 0f;
   
    void Start()
    {
        // Trava e esconde o cursor do mouse durante o gameplay.
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Le o movimento do mouse em cada eixo.
        float inputX = Input.GetAxis("Mouse X") * mouseSens;
        float inputY = Input.GetAxis("Mouse Y") * mouseSens;

        // Gira a camera para cima e baixo, limitando para nao virar alem do normal.
        camRotationY -= inputY;
        camRotationY = Mathf.Clamp(camRotationY, -90f, 90f);
        transform.localEulerAngles = Vector3.right * camRotationY;

        // Gira o corpo do jogador para esquerda e direita.
        player.Rotate(Vector3.up * inputX);
    }
}
