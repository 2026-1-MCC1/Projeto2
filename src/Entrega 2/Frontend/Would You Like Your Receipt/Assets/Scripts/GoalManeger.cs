using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GoalManager : MonoBehaviour
{
    // Este script controla as metas do jogo.
    // Ele conta clientes atendidos, produtos vendidos e mostra a tela final ao completar os objetivos.
    // Mantem uma referencia global para outros scripts encontrarem este gerenciador.
    public static GoalManager Instance { get; private set; }

    // OBJETIVOS
    public int clientesObjetivo = 10;
    public int produtosObjetivo = 5;

    // PROGRESSO
    private int clientesAtendidos = 0;
    private int produtosVendidos = 0;
    private bool objetivosCompletos;

    // UI (texto na tela)
    public TMP_Text textoObjetivos;
    // Guarda a mensagem temporaria que aparece abaixo dos objetivos.
    private string mensagemTemporaria = string.Empty;
    private Coroutine limparMensagemCoroutine;

    // BARRA DE PROGRESSO (opcional)
    public Image barraProgresso;
    GameObject telaFinalizacao;
    bool cursorVisivelAntesDaTelaFinal;
    CursorLockMode cursorLockAntesDaTelaFinal;

    private void Awake()
    {
        // Configura a instancia global e encontra o texto de objetivos na UI.
        // Garante que exista apenas um GoalManager ativo na cena.
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (textoObjetivos == null)
        {
            // Tenta achar automaticamente o texto da interface se o campo estiver vazio.
            GameObject goalTextObject = GameObject.Find("GoalText");
            if (goalTextObject != null)
            {
                textoObjetivos = goalTextObject.GetComponent<TMP_Text>();
            }
        }

        if (textoObjetivos == null)
        {
            Debug.LogWarning("GoalText was not found for GoalManager.", this);
        }
        else
        {
            // Libera espaco suficiente para mostrar objetivos e mensagens juntos.
            textoObjetivos.overflowMode = TextOverflowModes.Overflow;

            RectTransform textRect = textoObjetivos.rectTransform;
            if (textRect.sizeDelta.y < 200f)
            {
                // Aumenta a altura para caber varias linhas com informacoes do produto.
                textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, 200f);
            }
        }
    }

    private void Start()
    {
        // Mostra o estado inicial dos objetivos assim que a cena comeca.
        AtualizarInterface();
    }

    // CHAMAR quando atender cliente
    public void ClienteAtendido()
    {
        // Chamado pelo sistema do caixa quando o jogador entrega o troco correto.
        clientesAtendidos++;
        AtualizarInterface();
        ChecarObjetivos();
    }

    // CHAMAR quando vender produto
    public void ProdutoVendido()
    {
        // Chamado pelo scanner quando um produto valido e lido.
        produtosVendidos++;
        AtualizarInterface();
        ChecarObjetivos();
    }

    public void MostrarMensagem(string mensagem, float duracao = 2f)
    {
        // Mostra mensagens temporarias junto com os objetivos.
        // Troca a mensagem atual e atualiza o texto na tela imediatamente.
        mensagemTemporaria = mensagem;
        AtualizarInterface();

        if (limparMensagemCoroutine != null)
        {
            StopCoroutine(limparMensagemCoroutine);
        }

        if (duracao > 0f)
        {
            // Agenda a limpeza da mensagem depois de alguns segundos.
            limparMensagemCoroutine = StartCoroutine(LimparMensagemDepois(duracao));
        }
    }

    private IEnumerator LimparMensagemDepois(float duracao)
    {
        yield return new WaitForSeconds(duracao);
        // Remove a mensagem temporaria e volta a mostrar apenas os objetivos.
        mensagemTemporaria = string.Empty;
        limparMensagemCoroutine = null;
        AtualizarInterface();
    }

    private void AtualizarInterface()
    {
        if (textoObjetivos != null)
        {
            // Monta o texto principal com o progresso atual.
            string texto = "Clientes: " + clientesAtendidos + "/" + clientesObjetivo +
                           "\nProdutos: " + produtosVendidos + "/" + produtosObjetivo;

            if (!string.IsNullOrWhiteSpace(mensagemTemporaria))
            {
                // Acrescenta a mensagem do scanner ou do NPC abaixo dos objetivos.
                texto += "\n\n" + mensagemTemporaria;
            }

            textoObjetivos.text = texto;
        }

        AtualizarBarraProgresso();
    }

    private void AtualizarBarraProgresso()
    {
        if (barraProgresso == null)
        {
            return;
        }

        // Calcula o quanto dos objetivos totais ja foi concluido.
        float totalAtual = clientesAtendidos + produtosVendidos;
        float totalObjetivo = Mathf.Max(1f, clientesObjetivo + produtosObjetivo);
        barraProgresso.fillAmount = totalAtual / totalObjetivo;
    }

    private void ChecarObjetivos()
    {
        // Verifica se as duas metas foram concluidas ao mesmo tempo.
        if (objetivosCompletos)
        {
            return;
        }

        if (clientesAtendidos >= clientesObjetivo &&
            produtosVendidos >= produtosObjetivo)
        {
            // Evita repetir a mensagem de objetivo completo varias vezes.
            objetivosCompletos = true;
            MostrarMensagem("OBJETIVOS COMPLETOS!", 3f);
            MostrarTelaFinalizacao();
            Debug.Log("OBJETIVOS COMPLETOS!");
        }
    }

    void MostrarTelaFinalizacao()
    {
        // Abre a tela final, libera o mouse e garante que os botoes possam ser clicados.
        if (telaFinalizacao == null)
        {
            CriarTelaFinalizacao();
        }

        cursorVisivelAntesDaTelaFinal = Cursor.visible;
        cursorLockAntesDaTelaFinal = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GarantirEventSystem();
        telaFinalizacao.SetActive(true);
    }

    void CriarTelaFinalizacao()
    {
        // Cria toda a tela final por codigo para nao depender de prefab pronto na cena.
        GameObject canvasObject = new GameObject("TelaFinalizacaoCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 450f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GarantirEventSystem();

        telaFinalizacao = new GameObject("TelaFinalizacao");
        telaFinalizacao.transform.SetParent(canvasObject.transform, false);

        Image fundo = telaFinalizacao.AddComponent<Image>();
        fundo.color = new Color(0f, 0f, 0f, 0.86f);

        RectTransform telaRect = telaFinalizacao.GetComponent<RectTransform>();
        telaRect.anchorMin = Vector2.zero;
        telaRect.anchorMax = Vector2.one;
        telaRect.offsetMin = Vector2.zero;
        telaRect.offsetMax = Vector2.zero;

        GameObject painel = new GameObject("PainelFinalizacao");
        painel.transform.SetParent(telaFinalizacao.transform, false);

        RectTransform painelRect = painel.AddComponent<RectTransform>();
        painelRect.anchorMin = new Vector2(0.5f, 0.5f);
        painelRect.anchorMax = new Vector2(0.5f, 0.5f);
        painelRect.pivot = new Vector2(0.5f, 0.5f);
        painelRect.anchoredPosition = Vector2.zero;
        painelRect.sizeDelta = new Vector2(560f, 300f);

        Image painelFundo = painel.AddComponent<Image>();
        painelFundo.color = new Color(0.03f, 0.025f, 0.025f, 0.98f);

        CriarTextoFinalizacao(painel.transform, "Fim de expediente", new Vector2(0f, 88f), new Vector2(500f, 64f), 34f, TextAlignmentOptions.Center);
        CriarTextoFinalizacao(
            painel.transform,
            "Meta concluída. Você pode voltar ao menu ou continuar atendendo clientes.",
            new Vector2(0f, 22f),
            new Vector2(480f, 70f),
            20f,
            TextAlignmentOptions.Center
        );

        Button voltarMenu = CriarBotaoFinalizacao(painel.transform, "Voltar ao menu", VoltarAoMenu);
        PosicionarBotaoFinalizacao(voltarMenu, new Vector2(-130f, -86f), new Vector2(220f, 46f));

        Button continuar = CriarBotaoFinalizacao(painel.transform, "Continuar expediente", ContinuarExpediente);
        PosicionarBotaoFinalizacao(continuar, new Vector2(130f, -86f), new Vector2(240f, 46f));
    }

    TMP_Text CriarTextoFinalizacao(Transform parent, string texto, Vector2 posicao, Vector2 tamanho, float fonte, TextAlignmentOptions alinhamento)
    {
        GameObject textoObject = new GameObject("TextoFinalizacao");
        textoObject.transform.SetParent(parent, false);

        TMP_Text label = textoObject.AddComponent<TextMeshProUGUI>();
        label.text = texto;
        label.color = new Color(1f, 0.93f, 0.82f, 1f);
        label.fontStyle = FontStyles.Bold;
        label.fontSize = fonte;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = fonte;
        label.alignment = alinhamento;
        label.textWrappingMode = TextWrappingModes.Normal;

        RectTransform rect = textoObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;

        return label;
    }

    Button CriarBotaoFinalizacao(Transform parent, string texto, UnityEngine.Events.UnityAction acao)
    {
        // Cria um botao completo com imagem, texto e evento de clique.
        GameObject botaoObject = new GameObject(texto + "Button");
        botaoObject.transform.SetParent(parent, false);

        Image imagem = botaoObject.AddComponent<Image>();
        imagem.color = new Color(0.09f, 0.02f, 0.025f, 0.95f);
        imagem.raycastTarget = true;

        Button botao = botaoObject.AddComponent<Button>();
        botao.targetGraphic = imagem;
        botao.interactable = true;
        botao.onClick.AddListener(acao);

        ColorBlock cores = botao.colors;
        cores.normalColor = new Color(0.09f, 0.02f, 0.025f, 0.95f);
        cores.highlightedColor = new Color(0.45f, 0.04f, 0.05f, 1f);
        cores.pressedColor = new Color(0.7f, 0.07f, 0.06f, 1f);
        cores.selectedColor = cores.highlightedColor;
        cores.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        botao.colors = cores;

        Outline outline = botaoObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textoObject = new GameObject("Text (TMP)");
        textoObject.transform.SetParent(botaoObject.transform, false);

        TMP_Text label = textoObject.AddComponent<TextMeshProUGUI>();
        label.text = texto;
        label.color = new Color(1f, 0.93f, 0.82f, 1f);
        label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        label.fontSize = 19f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 20f;
        label.alignment = TextAlignmentOptions.Center;

        RectTransform textoRect = textoObject.GetComponent<RectTransform>();
        textoRect.anchorMin = Vector2.zero;
        textoRect.anchorMax = Vector2.one;
        textoRect.offsetMin = Vector2.zero;
        textoRect.offsetMax = Vector2.zero;
        label.raycastTarget = false;

        return botao;
    }

    void PosicionarBotaoFinalizacao(Button botao, Vector2 posicao, Vector2 tamanho)
    {
        RectTransform rect = botao.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
    }

    void VoltarAoMenu()
    {
        // Botao da tela final: volta para a cena MainMenu.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MainMenu");
    }

    void ContinuarExpediente()
    {
        // Botao da tela final: fecha o painel e devolve o controle ao jogador.
        if (telaFinalizacao != null)
        {
            telaFinalizacao.SetActive(false);
        }

        Cursor.lockState = cursorLockAntesDaTelaFinal;
        Cursor.visible = cursorVisivelAntesDaTelaFinal;
    }

    void GarantirEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            return;
        }

        eventSystem.gameObject.SetActive(true);
        eventSystem.enabled = true;

        StandaloneInputModule inputModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        inputModule.enabled = true;
    }
}

