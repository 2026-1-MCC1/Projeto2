using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    private bool movable = true;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            movable = !movable; //Alterna entre habilitar e desabilitar movimento do personagem (Para quando as câmeras estiverem abertas)
        }
        if(movable == true) //Apenas permite movimento se a variável estiver verdadeira.
        {
            float moveX = Input.GetAxis("Horizontal"); // A/D ou ? ?
            float moveZ = Input.GetAxis("Vertical");   // W/S ou ? ?

            Vector3 movement = new Vector3(moveX, 0f, moveZ);

            transform.Translate(movement * speed * Time.deltaTime);
        }
    }
}