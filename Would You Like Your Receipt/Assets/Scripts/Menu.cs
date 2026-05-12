using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    [Header("Textos editaveis")]
    [TextArea(3, 8)]
    [SerializeField] string textoCreditos =
        "Créditos\n\n" +
        "Jogo criado por: Guilherme da Silva Montes, Kris Pascali Janjiulio, Pietra Augusto Farias Ruiz. +
        "Programação: Guilherme da Silva Montes, Kris Pascali Janjiulio, Pietra Augusto Farias Ruiz." +
        "Arte e modelos: Sketchfab e Itch.io" ;

    [TextArea(3, 8)]
    [SerializeField] string textoComoJogar =
        "Como jogar\n\n" +
        "WASD: mover\n" +
        "Mouse: olhar ao redor\n" +
        "Clique esquerdo: pegar / soltar produtos\n" +
        "Clique direito: arremessar produto segurado\n" +
        "Scanner: passe o produto no caixa\n" +
        "Teclado numérico: digite o troco quando solicitado";

    [TextArea(3, 8)]
    [SerializeField] string textoSinopse =
        "Sinopse\n\n" +
        "Você trabalha no turno de um pequeno mercado onde cada cliente parece simples à primeira vista. " +
        "Entre caixas, produtos e recibos, a rotina fica cada vez mais estranha, e cabe a você atender todos sem perder o controle.";

    Canvas canvasMenu;
    GameObject grupoBotoesMenu;
    GameObject painelInfo;
    TMP_Text tituloInfo;
    TMP_Text corpoInfo;

    void Start()
    {
        canvasMenu = Object.FindFirstObjectByType<Canvas>();
        if (canvasMenu == null)
        {
            CriarCanvasMenu();
        }

        ConfigurarCanvasResponsivo();
        LimparMenuGeradoAnterior();
        MontarMenuPrincipal();
        CriarPainelInfo();
    }

    // Abre a cena principal quando o jogador clica em jogar.
    public void PlayGame()
    {
        SceneManager.LoadScene("Game");
    }

    // Fecha a aplicacao quando o jogador escolhe sair.
    public void QuitGame()
    {
        Debug.Log("Saiu do jogo");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void MostrarCreditos()
    {
        MostrarPainel("Créditos", textoCreditos);
    }

    public void MostrarComoJogar()
    {
        MostrarPainel("Como jogar", textoComoJogar);
    }

    public void MostrarSinopse()
    {
        MostrarPainel("Sinopse", textoSinopse);
    }

    public void FecharPainel()
    {
        if (painelInfo != null)
        {
            painelInfo.SetActive(false);
        }

        if (grupoBotoesMenu != null)
        {
            grupoBotoesMenu.SetActive(true);
        }
    }

    void MontarMenuPrincipal()
    {
        RectTransform grupo = CriarAreaBotoes();

        Button botaoJogar = EncontrarBotao("StartButton");
        Button botaoSair = EncontrarBotao("QuitButton");
        DesativarBotaoAntigo(botaoJogar);
        DesativarBotaoAntigo(botaoSair);

        Button jogar = CriarBotaoMenu(grupo, "Começar", PlayGame);
        Button comoJogar = CriarBotaoMenu(grupo, "Como jogar", MostrarComoJogar);
        Button sinopse = CriarBotaoMenu(grupo, "Sinopse", MostrarSinopse);
        Button creditos = CriarBotaoMenu(grupo, "Créditos", MostrarCreditos);
        Button sair = CriarBotaoMenu(grupo, "Sair", QuitGame);

        PosicionarBotao(jogar, new Vector2(-118f, 48f), new Vector2(190f, 38f));
        PosicionarBotao(sinopse, new Vector2(118f, 48f), new Vector2(190f, 38f));
        PosicionarBotao(comoJogar, new Vector2(-118f, -4f), new Vector2(190f, 38f));
        PosicionarBotao(creditos, new Vector2(118f, -4f), new Vector2(190f, 38f));
        PosicionarBotao(sair, new Vector2(0f, -56f), new Vector2(160f, 36f));
    }

    RectTransform CriarAreaBotoes()
    {
        GameObject grupoObject = new GameObject("MainMenuButtonArea");
        grupoBotoesMenu = grupoObject;
        grupoObject.transform.SetParent(canvasMenu.transform, false);

        RectTransform rect = grupoObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 72f);
        rect.sizeDelta = new Vector2(500f, 150f);

        return rect;
    }

    Button EncontrarBotao(string nome)
    {
        GameObject objeto = GameObject.Find(nome);
        return objeto != null ? objeto.GetComponent<Button>() : null;
    }

    void DesativarBotaoAntigo(Button botao)
    {
        if (botao != null)
        {
            botao.gameObject.SetActive(false);
        }
    }

    Button PrepararBotaoExistente(Button botao, RectTransform grupo, string texto)
    {
        if (botao == null)
        {
            if (texto == "Jogar" || texto == "Começar")
            {
                return CriarBotaoMenu(grupo, texto, PlayGame);
            }

            if (texto == "Sair")
            {
                return CriarBotaoMenu(grupo, texto, QuitGame);
            }

            return null;
        }

        botao.transform.SetParent(grupo, false);
        RectTransform rect = botao.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(230f, 54f);

        TMP_Text label = botao.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = texto;
            EstilizarTextoBotao(label);
        }

        EstilizarBotao(botao);
        return botao;
    }

    Button CriarBotaoMenu(Transform parent, string texto, UnityEngine.Events.UnityAction acao)
    {
        GameObject botaoObject = new GameObject(texto + "Button");
        botaoObject.transform.SetParent(parent, false);
        botaoObject.AddComponent<Image>();

        Button botao = botaoObject.AddComponent<Button>();
        botao.onClick.AddListener(acao);

        RectTransform rect = botaoObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(190f, 38f);

        GameObject textoObject = new GameObject("Text (TMP)");
        textoObject.transform.SetParent(botaoObject.transform, false);
        TMP_Text label = textoObject.AddComponent<TextMeshProUGUI>();

        RectTransform textoRect = textoObject.GetComponent<RectTransform>();
        textoRect.anchorMin = Vector2.zero;
        textoRect.anchorMax = Vector2.one;
        textoRect.offsetMin = Vector2.zero;
        textoRect.offsetMax = Vector2.zero;

        label.text = texto;
        EstilizarTextoBotao(label);
        EstilizarBotao(botao);
        return botao;
    }

    void PosicionarBotao(Button botao, Vector2 posicao, Vector2 tamanho)
    {
        if (botao == null)
        {
            return;
        }

        RectTransform rect = botao.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
    }

    void EstilizarBotao(Button botao)
    {
        if (botao == null)
        {
            return;
        }

        Image imagem = botao.GetComponent<Image>();
        if (imagem != null)
        {
            imagem.color = new Color(0.09f, 0.02f, 0.025f, 0.9f);
        }

        ColorBlock cores = botao.colors;
        cores.normalColor = new Color(0.09f, 0.02f, 0.025f, 0.9f);
        cores.highlightedColor = new Color(0.45f, 0.04f, 0.05f, 1f);
        cores.pressedColor = new Color(0.7f, 0.07f, 0.06f, 1f);
        cores.selectedColor = cores.highlightedColor;
        cores.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        cores.colorMultiplier = 1f;
        botao.colors = cores;

        Outline outline = botao.GetComponent<Outline>();
        if (outline == null)
        {
            outline = botao.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    void EstilizarTextoBotao(TMP_Text texto)
    {
        texto.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        texto.fontSize = 19f;
        texto.characterSpacing = 2f;
        texto.color = new Color(1f, 0.93f, 0.82f, 1f);
        texto.alignment = TextAlignmentOptions.Center;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 12f;
        texto.fontSizeMax = 20f;

        Shadow shadow = texto.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = texto.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    void CriarPainelInfo()
    {
        painelInfo = new GameObject("InfoPanel");
        painelInfo.transform.SetParent(canvasMenu.transform, false);
        painelInfo.transform.SetAsLastSibling();

        Image fundo = painelInfo.AddComponent<Image>();
        fundo.color = new Color(0f, 0f, 0f, 0.92f);

        RectTransform rect = painelInfo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        GameObject conteudo = new GameObject("InfoContent");
        conteudo.transform.SetParent(painelInfo.transform, false);

        RectTransform conteudoRect = conteudo.AddComponent<RectTransform>();
        conteudoRect.anchorMin = new Vector2(0.5f, 0.5f);
        conteudoRect.anchorMax = new Vector2(0.5f, 0.5f);
        conteudoRect.pivot = new Vector2(0.5f, 0.5f);
        conteudoRect.anchoredPosition = Vector2.zero;
        conteudoRect.sizeDelta = new Vector2(680f, 360f);

        Image conteudoFundo = conteudo.AddComponent<Image>();
        conteudoFundo.color = new Color(0.03f, 0.025f, 0.025f, 0.98f);

        tituloInfo = CriarTextoPainel(conteudo.transform, "Titulo", new Vector2(0f, 130f), new Vector2(600f, 54f), 32f, TextAlignmentOptions.Center);
        corpoInfo = CriarTextoPainel(conteudo.transform, "Corpo", new Vector2(0f, 12f), new Vector2(590f, 190f), 20f, TextAlignmentOptions.TopLeft);
        CriarBotaoFechar(conteudo.transform);

        painelInfo.SetActive(false);
    }

    TMP_Text CriarTextoPainel(Transform parent, string nome, Vector2 posicao, Vector2 tamanho, float fonte, TextAlignmentOptions alinhamento)
    {
        GameObject textoObject = new GameObject(nome);
        textoObject.transform.SetParent(parent, false);

        TMP_Text texto = textoObject.AddComponent<TextMeshProUGUI>();
        texto.color = new Color(0.95f, 0.94f, 0.88f, 1f);
        texto.fontSize = fonte;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 12f;
        texto.fontSizeMax = fonte;
        texto.alignment = alinhamento;
        texto.textWrappingMode = TextWrappingModes.Normal;

        RectTransform rect = textoObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;

        return texto;
    }

    void CriarBotaoFechar(Transform parent)
    {
        Button botao = CriarBotaoMenu(parent, "Voltar", FecharPainel);
        RectTransform rect = botao.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 36f);
        rect.sizeDelta = new Vector2(180f, 38f);
    }

    void MostrarPainel(string titulo, string corpo)
    {
        if (painelInfo == null)
        {
            CriarPainelInfo();
        }

        tituloInfo.text = titulo;
        corpoInfo.text = corpo;
        painelInfo.SetActive(true);
        painelInfo.transform.SetAsLastSibling();

        if (grupoBotoesMenu != null)
        {
            grupoBotoesMenu.SetActive(false);
        }
    }

    void CriarCanvasMenu()
    {
        GameObject canvasObject = new GameObject("Canvas");
        canvasMenu = canvasObject.AddComponent<Canvas>();
        canvasMenu.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    void ConfigurarCanvasResponsivo()
    {
        CanvasScaler scaler = canvasMenu.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasMenu.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 450f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
    }

    void LimparMenuGeradoAnterior()
    {
        DestruirObjetoGerado("MainMenuButtonArea");
        DestruirObjetoGerado("InfoPanel");
    }

    void DestruirObjetoGerado(string nome)
    {
        GameObject objeto = GameObject.Find(nome);
        if (objeto != null)
        {
            Destroy(objeto);
        }
    }
}
