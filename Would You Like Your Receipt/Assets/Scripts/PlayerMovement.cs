using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 3f;
    private bool movable = true;
    public CameraConsole console; //Permite selecionar GameObject do script no inspetor da Unity para referenciar depois.

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
        if (console != null && Input.GetKeyDown(console.OpenCameras))
        {
            movable = !movable; //Alterna entre habilitar e desabilitar movimento do personagem (Para quando as câmeras estiverem abertas)
        }
        if(movable == true) //Apenas permite movimento se a variável estiver verdadeira.
        {
            float moveX = Input.GetAxis("Horizontal"); // A/D
            float moveZ = Input.GetAxis("Vertical");   // W/S

            Vector3 movement = new Vector3(moveX, 0f, moveZ);

            transform.Translate(movement * speed * Time.deltaTime);
        }
    }
}
