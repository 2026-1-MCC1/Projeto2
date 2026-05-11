using UnityEngine;
using UnityEngine.UI;

public class PlayerCam : MonoBehaviour
{
    // Este script controla a visao em primeira pessoa.
    // Ele tambem cria uma mira simples para ajudar o jogador a saber onde esta clicando.
    // Script responsavel por girar a camera em primeira pessoa.
    // Corpo do jogador que gira junto com a camera no eixo horizontal.
    public Transform player;
    // Sensibilidade do mouse para controlar a camera.
    public float mouseSens = 2f;
    // Mostra uma mira simples no centro da tela para orientar os cliques.
    [SerializeField] bool mostrarCrosshair = true;
    [SerializeField] Color corCrosshair = Color.white;
    [SerializeField] Color corSombraCrosshair = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] float tamanhoCrosshair = 12f;
    [SerializeField] float espessuraCrosshair = 2f;
    // Rotacao vertical acumulada da camera.
    float camRotationY = 0f;

    void Start()
    {
        // Trava e esconde o cursor do mouse durante o gameplay.
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (mostrarCrosshair)
        {
            CriarCrosshair();
        }
    }

    void Update()
    {
        // O movimento horizontal gira o corpo do jogador.
        // O movimento vertical gira apenas a camera e e limitado para nao virar de cabeca para baixo.
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

    void CriarCrosshair()
    {
        // Cria a mira por codigo para nao depender de um objeto pronto na cena.
        if (GameObject.Find("CrosshairCanvas") != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("CrosshairCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        CriarLinhaCrosshair(canvasObject.transform, "HorizontalShadow", new Vector2(tamanhoCrosshair + 2f, espessuraCrosshair + 2f), corSombraCrosshair);
        CriarLinhaCrosshair(canvasObject.transform, "VerticalShadow", new Vector2(espessuraCrosshair + 2f, tamanhoCrosshair + 2f), corSombraCrosshair);
        CriarLinhaCrosshair(canvasObject.transform, "Horizontal", new Vector2(tamanhoCrosshair, espessuraCrosshair), corCrosshair);
        CriarLinhaCrosshair(canvasObject.transform, "Vertical", new Vector2(espessuraCrosshair, tamanhoCrosshair), corCrosshair);
    }

    void CriarLinhaCrosshair(Transform parent, string nome, Vector2 tamanho, Color cor)
    {
        // Cada linha da mira e uma pequena Image de UI.
        // Sao criadas duas linhas de sombra e duas linhas brancas principais.
        GameObject linha = new GameObject(nome);
        linha.transform.SetParent(parent, false);

        Image imagem = linha.AddComponent<Image>();
        imagem.color = cor;
        imagem.raycastTarget = false;

        RectTransform rect = linha.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = tamanho;
    }
}
