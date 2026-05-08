using UnityEngine;

public class CameraConsole : MonoBehaviour
{
    // Lista de cameras de seguranca que podem ser vistas pelo console.
    public GameObject[] Cameras;
    // Indice da camera atualmente selecionada.
    public int CurrentCam;
    // Tecla usada para abrir ou fechar o console de cameras.
    public KeyCode OpenCameras = KeyCode.Space;
    // Indica se o jogador esta olhando pelas cameras do console.
    public bool CamerasOpen;
    // Camera principal do jogador.
    public GameObject MainCamera;
    // Contador usado para impedir troca rapida demais entre cameras.
    public float CoolDownTimer;
    // Tempo minimo entre uma troca de camera e outra.
    public float CoolDownTime = 0.5f;

    void Start()
    {
        // Desativa todas as cameras de seguranca ao iniciar a cena.
        for (int i = 0; i < Cameras.Length; i++)
        {
            Cameras[i].SetActive(false);
        }

        // Garante que a visao normal do jogador comece ativa.
        MainCamera.SetActive(true);
    }

    
    void Update()
    {
        // Abre ou fecha a tela de cameras.
        if (Input.GetKeyDown(OpenCameras))
        {
            CamerasOpen = !CamerasOpen;
            ShowCamera();
        }

        // Permite trocar de camera somente quando o cooldown terminou.
        if (CoolDownTimer <= 0)
        {
            // Vai para a proxima camera da lista.
            if (Input.GetAxis("Horizontal") > 0)
            {
                Cameras[CurrentCam].SetActive(false);
                CurrentCam = CurrentCam + 1;
                if (CurrentCam >= Cameras.Length)
                {
                    CurrentCam = 0;
                }
                GoToCamera(CurrentCam);
                CoolDownTimer = CoolDownTime;
            }
            // Vai para a camera anterior da lista.
            else if (Input.GetAxis("Horizontal") < 0)
            {
                Cameras[CurrentCam].SetActive(false);
                CurrentCam = CurrentCam - 1;
                if (CurrentCam < 0)
                {
                    CurrentCam = Cameras.Length - 1;
                }
                GoToCamera(CurrentCam);
                CoolDownTimer = CoolDownTime;
            }
        }
        else
        {
            // Reduz o tempo restante ate a proxima troca ser liberada.
            CoolDownTimer -= Time.deltaTime;
        }
    }
    private void ShowCamera()
    {
        // Liga o console de cameras e libera o cursor para interacao.
        if (CamerasOpen)
        {
            Cameras[CurrentCam].SetActive(true);
            MainCamera.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        // Volta para a camera principal do jogador.
        else
        {
            Cameras[CurrentCam].SetActive(false);
            MainCamera.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void GoToCamera(int Progression)
    {
        // Desliga a camera atual antes de ativar a nova escolhida.
        Cameras[CurrentCam].SetActive(false);
        CurrentCam = Progression;
        ShowCamera();
    }
}

