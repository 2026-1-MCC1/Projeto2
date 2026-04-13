using UnityEngine;

public class CameraConsole : MonoBehaviour
{
    public GameObject[] Cameras;
    public int CurrentCam;
    public KeyCode OpenCameras = KeyCode.Space;
    public bool CamerasOpen;
    public GameObject MainCamera;
    public float CoolDownTimer;
    public float CoolDownTime = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < Cameras.Length; i++) //Faz com que as câmeras sejam desativadas quando o jogo inicia. Se começarem desativadas, não podem ser ativadas depois.
        {
            Cameras[i].SetActive(false);
        }
        MainCamera.SetActive(true); //Garante que a câmera principal (Visão do personagem) inicia ativa.
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(OpenCameras)) //Permite seleção de tecla para abrir e fechar câmera.
        {
            CamerasOpen = !CamerasOpen;
            ShowCamera();
        }

        if (CoolDownTimer <= 0) //Faz com que as câmeras apenas troquem se o cooldown for zero ou menor.
        {
            if (Input.GetAxis("Horizontal") > 0) //Vai para a próxima câmera na lista.
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
            else if (Input.GetAxis("Horizontal") < 0) //Vai para a câmera anterior na lista.
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
            CoolDownTimer -= Time.deltaTime; //Diminui o cooldown de troca de câmeras.
        }
    }
    private void ShowCamera()
    {
        if (CamerasOpen) //Desliga a câmera principal (Visão do personagem) ao abrir a tela de câmeras.
        {
            Cameras[CurrentCam].SetActive(true);
            MainCamera.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else //Ativa a câmera principal ao fechar a tela de câmeras.
        {
            Cameras[CurrentCam].SetActive(false);
            MainCamera.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    public void GoToCamera(int Progression) //Define progressão para movimentação de câmeras quando usando A e D.
    {
        Cameras[CurrentCam].SetActive(false);
        CurrentCam = Progression;
        ShowCamera();
    }
}
