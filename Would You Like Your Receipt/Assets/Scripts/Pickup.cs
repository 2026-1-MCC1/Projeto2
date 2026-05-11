using System.Globalization;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    // Este script controla produtos que o jogador pode pegar, arrastar, arremessar e escanear.
    // Ele tambem guarda informacoes do produto, como nome, codigo, preco e descricao.
    // Raiz opcional do produto completo. Use quando o Pickup estiver em um child do modelo.
    [SerializeField] Transform produtoCompletoRoot;

    // Forca aplicada quando o jogador arremessa o produto.
    [SerializeField] float throwForce = 150f;
    // Distancia maxima para pegar ou continuar segurando o produto.
    [SerializeField] float maxDistance = 4.5f;
    // Nome exibido na tela quando o produto for escaneado.
    [SerializeField] string nomeProduto = "";
    // Codigo opcional do produto.
    [SerializeField] string codigoProduto = "";
    // Preco exibido na tela quando o produto for escaneado.
    [SerializeField] float precoProduto;
    // Descricao curta do produto para a tela do scanner.
    [SerializeField] string descricaoProduto = "";
    // Define se o item deve sumir depois de ser escaneado.
    [SerializeField] bool destruirAposEscanear = true;
    // Mensagem opcional que aparece quando este item e escaneado.
    [SerializeField] string mensagemEscaneamento = "";
    // Tempo padrao de exibicao da mensagem do produto.
    [SerializeField] float duracaoMensagemEscaneamento = 2f;
    // Mantem os produtos da prateleira parados sem trocar collider, trigger ou formato.
    [SerializeField] bool iniciarTravadoNaPrateleira = true;
    // Permite selecionar o console de cameras no Inspector.
    public CameraConsole console;

    // Marca se o item esta preso ao ponto de segurar do jogador.
    bool estaSegurando;
    // Impede que o mesmo item seja lido mais de uma vez.
    bool foiEscaneado;
    // Marca copias que nasceram no caixa para serem removidas depois da venda.
    bool produtoDoCaixa;
    // Define se o jogador pode pegar e escanear este item neste momento.
    bool permiteInteracaoJogador = true;
    // Marca itens que funcionam apenas como representacao visual da prateleira.
    bool produtoVisualDaLoja;
    // Tempo minimo ate o item voltar a poder ser lido pelo scanner.
    float instanteLiberadoParaScanner;
    // Indica se o item esta travado no checkout aguardando o clique do jogador.
    bool travadoNoCaixa;

    // Ponto temporario onde o item fica enquanto esta sendo carregado.
    TempParent tempParent;
    // Rigidbody principal do produto.
    Rigidbody rb;
    // Raiz real do asset inteiro que deve ser movida, destruida e instanciada.
    Transform raizProduto;

    private void Awake()
    {
        // Awake roda antes do Start e garante que a raiz do produto ja esteja resolvida.
        // Resolve a raiz logo no inicio para os outros sistemas usarem a hierarquia correta.
        raizProduto = ResolverRaizProduto();
    }

    private void Start()
    {
        // Busca as referencias locais usadas no fluxo de pegar e soltar.
        rb = ObterRigidbodyDoProduto();
        tempParent = TempParent.Instance;

        if (iniciarTravadoNaPrateleira && !produtoDoCaixa)
        {
            TravarFisicaSemAlterarColisao();
        }

        if (console == null)
        {
            // Recupera a configuracao do console caso o campo esteja vazio no Inspector.
            console = Object.FindFirstObjectByType<CameraConsole>();
        }

        if (console == null)
        {
            Debug.LogWarning("CameraConsole was not found for Pickup.", this);
        }
    }

    private void Update()
    {
        // Enquanto o jogador segura o item, o objeto segue o ponto TempParent.
        // Mantem a logica de arraste ativa apenas enquanto o jogador segura o item.
        if (estaSegurando)
        {
            Segurar();
        }
    }

    private void OnMouseDown()
    {
        // Encaminha o clique para a mesma rotina usada pelos proxies.
        IniciarSegurarPeloProxy();
    }

    private void OnMouseUp()
    {
        // Solta o item quando o botao do mouse for liberado.
        SoltarPeloProxy();
    }

    private void OnMouseExit()
    {
        // Nao soltamos aqui para permitir arrastar sem perder o item no meio do movimento.
    }

    public void IniciarSegurarPeloProxy()
    {
        // Entrada principal para pegar um produto.
        // Pode ser chamada pelo proprio objeto ou por um PickupProxy em collider filho.
        if (!permiteInteracaoJogador)
        {
            return;
        }

        if (tempParent == null)
        {
            Debug.Log("TempParent item not found in scene!");
            return;
        }

        Transform raiz = ObterRaizProduto();
        if (raiz == null)
        {
            return;
        }

        // So permite pegar o item quando ele estiver perto o bastante da mao do jogador.
        float distance = CalcularMenorDistanciaAteMao(raiz);
        if (distance > maxDistance)
        {
            return;
        }

        estaSegurando = true;

        if (travadoNoCaixa)
        {
            // Desprende o item do checkout antes de leva-lo para a mao do jogador.
            raiz.SetParent(null);
        }

        if (rb != null)
        {
            // Desliga a gravidade enquanto o item esta sendo segurado.
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        travadoNoCaixa = false;
        raiz.SetParent(tempParent.transform);
    }

    public void SoltarPeloProxy()
    {
        // Reaproveita a mesma rotina de soltura para clique direto e proxy.
        Soltar();
    }

    public bool TentarIniciarEscaneamento()
    {
        // O scanner chama esta funcao antes de contar o produto.
        // Ela impede leitura duplicada e bloqueia itens que ainda nao estao liberados.
        if (!permiteInteracaoJogador)
        {
            return false;
        }

        if (!PodeSerEscaneadoAgora() || foiEscaneado)
        {
            return false;
        }

        // Marca o item como lido para impedir escaneamentos duplicados.
        foiEscaneado = true;

        // Solta o item antes de finalizar a leitura para nao misturar o scanner com o drag.
        Soltar();
        return true;
    }

    public void IgnorarScannerPorSegundos(float duracao)
    {
        // Evita autoescaneamento no mesmo instante em que o item aparece no caixa.
        instanteLiberadoParaScanner = Time.time + Mathf.Max(0f, duracao);
    }

    public bool PodeSerEscaneadoAgora()
    {
        if (travadoNoCaixa)
        {
            // Enquanto o item estiver fixo no checkout, ele nao pode se escanear sozinho.
            return false;
        }

        return Time.time >= instanteLiberadoParaScanner;
    }

    public Transform ObterRaizProduto()
    {
        // Entrega a raiz completa para os sistemas que lidam com clones e fisica.
        if (raizProduto == null)
        {
            raizProduto = ResolverRaizProduto();
        }

        return raizProduto;
    }

    public void DefinirRaizDoProduto(Transform raizCompleta)
    {
        // Permite que o configurador automatico diga qual Transform representa o produto inteiro.
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();
        rb = ObterRigidbodyDoProduto();
    }

    public void FixarNoCaixa()
    {
        // Mantem o item parado no checkout ate o jogador clicar nele.
        travadoNoCaixa = true;

        if (rb != null)
        {
            PrepararRigidbodyKinematico();
        }
    }

    public void ConfigurarComoProdutoDoCaixa(Transform raizCompleta)
    {
        // Configura clones que aparecem no caixa ou sao carregados pelo NPC.
        // Marca o clone como item de checkout e reseta os estados de leitura.
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();
        rb = ObterRigidbodyDoProduto();
        produtoDoCaixa = true;
        produtoVisualDaLoja = false;
        permiteInteracaoJogador = true;
        foiEscaneado = false;
        travadoNoCaixa = false;
    }

    public void ConfigurarComoProdutoVisualDaLoja(Transform raizCompleta)
    {
        // Configura produtos decorativos da loja que o NPC pode visitar, mas o jogador nao pega diretamente.
        // Mantem o item visivel na loja, mas sem permitir pegar ou escanear antes do caixa.
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();
        rb = ObterRigidbodyDoProduto();
        produtoDoCaixa = false;
        produtoVisualDaLoja = true;
        permiteInteracaoJogador = false;
        foiEscaneado = false;
        estaSegurando = false;
        travadoNoCaixa = false;

        if (rb != null)
        {
            // Produto visual da loja fica parado, sem gravidade, ate o NPC "retira-lo" da prateleira.
            PrepararRigidbodyKinematico();
        }
    }

    public void ConfigurarComoProdutoDaLojaInteragivel(Transform raizCompleta)
    {
        // Configura produtos dentro de Interactables.
        // Eles continuam clicaveis pelo jogador e tambem podem ser escolhidos pelo NPC.
        // Mantem o produto como alvo da loja sem bloquear o clique do jogador.
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();
        rb = ObterRigidbodyDoProduto();
        produtoDoCaixa = false;
        produtoVisualDaLoja = false;
        permiteInteracaoJogador = true;
        foiEscaneado = false;
        estaSegurando = false;
        travadoNoCaixa = false;

        TravarFisicaSemAlterarColisao();
    }

    public void LiberarInteracaoDaLoja(Transform raizCompleta)
    {
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();
        rb = ObterRigidbodyDoProduto();
        produtoDoCaixa = false;
        produtoVisualDaLoja = false;
        permiteInteracaoJogador = true;
        travadoNoCaixa = false;
    }

    public void EsconderProdutoVisualDaLoja()
    {
        if (!produtoVisualDaLoja)
        {
            return;
        }

        Transform raiz = ObterRaizProduto();
        if (raiz != null)
        {
            // Simula o NPC tirando o item da prateleira antes de levar para o caixa.
            raiz.gameObject.SetActive(false);
        }
    }

    public void MostrarProdutoVisualDaLoja()
    {
        if (!produtoVisualDaLoja)
        {
            return;
        }

        Transform raiz = ObterRaizProduto();
        if (raiz != null)
        {
            // Deixa o produto pronto para reaparecer quando voce quiser fazer o reestoque.
            raiz.gameObject.SetActive(true);
        }
    }

    public string ObterMensagemEscaneamento()
    {
        // Monta a mensagem completa que aparece na UI do scanner quando o produto e lido.
        // Monta o texto completo com os dados do item para o Canvas do scanner.
        string mensagem = "Produto: " + ObterNomeProduto();

        if (!string.IsNullOrWhiteSpace(codigoProduto))
        {
            mensagem += "\nCodigo: " + codigoProduto;
        }

        if (precoProduto > 0f)
        {
            mensagem += "\nPreco: R$ " + precoProduto.ToString("F2", CultureInfo.GetCultureInfo("pt-BR"));
        }

        if (!string.IsNullOrWhiteSpace(descricaoProduto))
        {
            mensagem += "\nInfo: " + descricaoProduto;
        }

        if (!string.IsNullOrWhiteSpace(mensagemEscaneamento))
        {
            mensagem += "\n\n" + mensagemEscaneamento;
        }

        return mensagem;
    }

    public string ObterNomeProduto()
    {
        // Usa o nome configurado no Inspector ou cai no nome do objeto como fallback.
        return string.IsNullOrWhiteSpace(nomeProduto) ? gameObject.name : nomeProduto;
    }

    public string ObterCodigoProduto()
    {
        // Entrega o codigo puro para sistemas que precisem casar produtos automaticamente.
        return codigoProduto;
    }

    public float ObterPrecoProduto()
    {
        // Entrega o preco para o sistema do caixa calcular o troco.
        return precoProduto;
    }

    public float ObterDuracaoMensagemEscaneamento()
    {
        // Define por quanto tempo a mensagem do item fica visivel no scanner.
        return duracaoMensagemEscaneamento;
    }

    public void FinalizarEscaneamento()
    {
        if (produtoDoCaixa || destruirAposEscanear)
        {
            // Remove o item vendido para nao deixar sobras no checkout.
            Transform raiz = ObterRaizProduto();
            Destroy(raiz != null ? raiz.gameObject : gameObject);
        }
    }

    private void Segurar()
    {
        // Mantem o item preso na mao do jogador e verifica se ele deve soltar.
        if (tempParent == null)
        {
            Soltar();
            return;
        }

        Transform raiz = ObterRaizProduto();
        if (raiz == null)
        {
            Soltar();
            return;
        }

        float distance = Vector3.Distance(raiz.position, tempParent.transform.position);

        // Solta o item se ele se afastar demais da ancora de segurar.
        if (distance >= maxDistance)
        {
            Soltar();
            return;
        }

        // Solta o item se o jogador abrir o console de cameras.
        if (console != null && Input.GetKeyDown(console.OpenCameras))
        {
            Soltar();
            return;
        }

        if (rb != null)
        {
            // Zera a fisica para o item acompanhar a mao sem vibracao.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (Input.GetMouseButtonDown(1))
        {
            // Arremessa o item para frente e encerra o arraste.
            if (rb != null)
            {
                rb.AddForce(tempParent.transform.forward * throwForce);
            }

            Soltar();
        }
    }

    private void Soltar()
    {
        // Devolve o item para o mundo fisico quando o jogador solta o mouse.
        if (!estaSegurando)
        {
            return;
        }

        // Solta o item e devolve o controle fisico para o mundo.
        estaSegurando = false;

        Transform raiz = ObterRaizProduto();
        if (raiz != null)
        {
            raiz.SetParent(null);
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    private Transform ResolverRaizProduto()
    {
        // Define qual Transform representa o produto inteiro.
        // Isso e importante para modelos com varios filhos e colliders separados.
        // Quando uma raiz foi definida manualmente, ela sempre vence.
        if (produtoCompletoRoot != null)
        {
            return produtoCompletoRoot;
        }

        // Fallback para itens simples onde o Pickup ja esta no objeto principal.
        return transform;
    }

    private Rigidbody ObterRigidbodyDoProduto()
    {
        Transform raiz = ObterRaizProduto();
        if (raiz == null)
        {
            return GetComponent<Rigidbody>();
        }

        // Aceita Rigidbody na raiz ou em qualquer child do modelo importado.
        Rigidbody rigidbodyRaiz = raiz.GetComponent<Rigidbody>();
        return rigidbodyRaiz != null ? rigidbodyRaiz : raiz.GetComponentInChildren<Rigidbody>();
    }

    float CalcularMenorDistanciaAteMao(Transform raiz)
    {
        // Usa os colliders do produto para calcular uma distancia mais justa ate a mao.
        // Assim produtos grandes podem ser pegos pela borda, nao apenas pelo centro.
        if (tempParent == null)
        {
            return float.MaxValue;
        }

        Vector3 pontoMao = tempParent.transform.position;
        float menorDistancia = raiz != null ? Vector3.Distance(raiz.position, pontoMao) : float.MaxValue;

        if (raiz == null)
        {
            return menorDistancia;
        }

        Collider[] colliders = raiz.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider colliderAtual = colliders[i];
            if (colliderAtual == null || !colliderAtual.enabled)
            {
                continue;
            }

            Vector3 pontoMaisProximo = colliderAtual.ClosestPoint(pontoMao);
            float distanciaCollider = Vector3.Distance(pontoMaisProximo, pontoMao);
            if (distanciaCollider < menorDistancia)
            {
                menorDistancia = distanciaCollider;
            }
        }

        return menorDistancia;
    }

    void PrepararRigidbodyKinematico()
    {
        // Trava o Rigidbody para o item ficar parado no caixa ou na prateleira.
        if (rb == null)
        {
            return;
        }

        if (!rb.isKinematic)
        {
            // Zera a fisica antes de travar o corpo para evitar warnings da Unity.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = true;
    }

    void TravarFisicaSemAlterarColisao()
    {
        // Mantem o produto parado sem mudar formato, trigger ou tamanho dos colliders.
        if (rb == null)
        {
            return;
        }

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = true;
    }
}
