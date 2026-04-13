using UnityEngine;

public class PlayerCam : MonoBehaviour
{
    public Transform player;
    public float mouseSens = 2f;
    float camRotationY = 0f;
   
    void Start()
    {
        //Trava e esconde o cursor do mouse
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        //Input do mouse
        float inputX = Input.GetAxis("Mouse X") * mouseSens;
        float inputY = Input.GetAxis("Mouse Y") * mouseSens;

        //Rotação da câmera para cima e baixo
        camRotationY -= inputY;
        camRotationY = Mathf.Clamp(camRotationY, -90f, 90f);
        transform.localEulerAngles = Vector3.right * camRotationY;

        //Rotação da câmera e do objeto para os lados
        player.Rotate(Vector3.up * inputX);
    }
}
