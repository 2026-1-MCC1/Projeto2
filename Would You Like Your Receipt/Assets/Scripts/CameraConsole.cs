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

    private void Start()
    {
        // Desliga todas as cameras secundarias e deixa apenas a visao principal ligada.
        DesativarTodasAsCameras();

        if (MainCamera != null)
        {
            MainCamera.SetActive(true);
        }
    }

    private void Update()
    {
        // Abre ou fecha a tela de cameras.
        if (Input.GetKeyDown(OpenCameras))
        {
            CamerasOpen = !CamerasOpen;
            ShowCamera();
        }

        // Sem cameras configuradas, o resto do fluxo nao precisa rodar.
        if (Cameras == null || Cameras.Length == 0)
        {
            return;
        }

        if (CoolDownTimer > 0f)
        {
            // Reduz o tempo restante ate a proxima troca ser liberada.
            CoolDownTimer -= Time.deltaTime;
            return;
        }

        // Permite trocar de camera somente quando o cooldown terminou.
        if (Input.GetAxis("Horizontal") > 0f)
        {
            GoToCamera(CurrentCam + 1);
        }
        else if (Input.GetAxis("Horizontal") < 0f)
        {
            GoToCamera(CurrentCam - 1);
        }
    }

    private void ShowCamera()
    {
        // Sem cameras configuradas, apenas garante que a camera principal fique ativa.
        if (Cameras == null || Cameras.Length == 0)
        {
            if (MainCamera != null)
            {
                MainCamera.SetActive(true);
            }

            return;
        }

        CurrentCam = Mathf.Clamp(CurrentCam, 0, Cameras.Length - 1);

        if (CamerasOpen)
        {
            // Liga a camera escolhida do console e libera o cursor para interacao.
            Cameras[CurrentCam].SetActive(true);

            if (MainCamera != null)
            {
                MainCamera.SetActive(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Volta para a camera principal e fecha a visao do console.
            Cameras[CurrentCam].SetActive(false);

            if (MainCamera != null)
            {
                MainCamera.SetActive(true);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void GoToCamera(int progression)
    {
        if (Cameras == null || Cameras.Length == 0)
        {
            return;
        }

        // Desliga a camera atual antes de ativar a proxima quando o console estiver aberto.
        if (CamerasOpen)
        {
            Cameras[CurrentCam].SetActive(false);
        }

        CurrentCam = ModuloCircular(progression, Cameras.Length);
        CoolDownTimer = CoolDownTime;
        ShowCamera();
    }

    private void DesativarTodasAsCameras()
    {
        if (Cameras == null)
        {
            return;
        }

        for (int i = 0; i < Cameras.Length; i++)
        {
            if (Cameras[i] != null)
            {
                Cameras[i].SetActive(false);
            }
        }
    }

    private int ModuloCircular(int valor, int total)
    {
        // Garante que o indice volte para o inicio ou para o fim sem estourar o array.
        if (total <= 0)
        {
            return 0;
        }

        int resultado = valor % total;
        return resultado < 0 ? resultado + total : resultado;
    }
}
