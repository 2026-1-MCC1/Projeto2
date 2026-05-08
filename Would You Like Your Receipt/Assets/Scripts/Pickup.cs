using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    // Lista global dos espacos de prateleira que podem receber reposicao vinda do estoque.
    static readonly List<Pickup> produtosDePrateleiraRegistrados = new List<Pickup>();

    // Raiz opcional do produto completo. Use quando o Pickup estiver em uma tampa/child do asset.
    [SerializeField] Transform produtoCompletoRoot;
    // Distancia maxima para encaixar um item do estoque no lugar vazio da prateleira.
    [SerializeField] float distanciaParaReporNaPrateleira = 1.2f;

    // Controla se o produto esta preso ao ponto de segurar do jogador.
    bool estaSegurando = false;
    // Impede que o mesmo produto conte duas vezes no scanner.
    bool foiEscaneado = false;
    // Marca se este produto veio da prateleira e pode voltar para ela.
    bool produtoDePrateleira = false;
    // Marca se este item fica no estoque e serve para repor uma prateleira vazia.
    bool produtoDeEstoque = false;
    // Marca copias que foram levadas ao caixa e devem sumir depois do escaneamento.
    bool produtoDoCaixa = false;
    // Indica se o produto esta fisicamente no lugar original da prateleira.
    bool estaNaPrateleira = false;
    // Impede outro NPC de escolher o mesmo produto enquanto ele esta em atendimento.
    bool reservadoPorNpc = false;

    // Forca aplicada quando o jogador arremessa o produto.
    [SerializeField] float throwForce = 150f;
    // Distancia maxima para pegar ou continuar segurando o produto.
    [SerializeField] float maxDistance = 3f;
    // Nome exibido na tela quando o produto for escaneado.
    [SerializeField] string nomeProduto = "";
    // Codigo opcional do produto.
    [SerializeField] string codigoProduto = "";
    // Preco exibido na tela quando o produto for escaneado.
    [SerializeField] float precoProduto = 0f;
    // Descricao curta do produto para a tela do scanner.
    [SerializeField] string descricaoProduto = "";
    // Define se o item deve sumir depois de ser escaneado.
    [SerializeField] bool destruirAposEscanear = true;
    // Mensagem opcional que aparece quando este item e escaneado.
    [SerializeField] string mensagemEscaneamento = "";
    [SerializeField] float duracaoMensagemEscaneamento = 2f;

    public CameraConsole console; // Permite selecionar o console de cameras no Inspector.

    // Ponto temporario onde o produto fica como filho enquanto esta sendo carregado.
    TempParent tempParent;
    // Rigidbody usado para ativar/desativar gravidade e zerar movimentos.
    Rigidbody rb;
    // Guarda a posicao atual antes de soltar o objeto.
    Vector3 objPosition;
    // Raiz resolvida do produto completo usado para mover, esconder ou destruir o item inteiro.
    Transform raizProduto;
    // Dados originais usados para encaixar o produto de volta na prateleira.
    Transform prateleiraOriginal;
    Vector3 posicaoLocalOriginal;
    Quaternion rotacaoLocalOriginal;
    Vector3 escalaLocalOriginal;
    string chaveProduto = string.Empty;

    private void Awake()
    {
        // Resolve a raiz do produto antes de outros sistemas tentarem usar este item.
        raizProduto = ResolverRaizProduto();
    }

    void Start()
    {
        // Pega as referencias necessarias para o comportamento de carregar item.
        rb = ObterRigidbodyDoProduto();
        tempParent = TempParent.Instance;

        if (console == null)
        {
            // Recupera a configuracao do console de cameras caso o campo esteja vazio.
            console = Object.FindFirstObjectByType<CameraConsole>();
        }

        if (console == null)
        {
            Debug.LogWarning("CameraConsole was not found for Pickup.", this);
        }
    }

    void Update()
    {
        // Mantem as regras de segurar enquanto o item esta na mao do jogador.
        if (estaSegurando)
        {
            Segurar();
        }
    }

    private void OnMouseDown()
    {
        // Pega o item se ele estiver perto o bastante do jogador.
        if (tempParent != null)
        {
            float distance = Vector3.Distance(transform.position, tempParent.transform.position);
            if (distance <= maxDistance && raizProduto != null)
            {
                estaSegurando = true;

                if (rb != null)
                {
                    rb.useGravity = false;
                    rb.detectCollisions = true;
                }

                raizProduto.SetParent(tempParent.transform);
            }
        }
        else
        {
            Debug.Log("TempParent item not found in scene!");
        }
    }

    private void OnMouseUp()
    {
        // Solta o item ao soltar o botao do mouse.
        Soltar();
    }

    private void OnMouseExit()
    {
        // Solta o item quando o mouse sai do objeto.
        Soltar();
    }

    private void Segurar()
    {
        float distance = Vector3.Distance(transform.position, tempParent.transform.position);

        // Solta o item se ele ficar longe demais do jogador.
        if (distance >= maxDistance)
        {
            Soltar();
        }

        // Solta o item quando o console de cameras for aberto.
        if (console != null && Input.GetKeyDown(console.OpenCameras))
        {
            Soltar();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Joga o item para frente ao clicar com o botao direito.
        if (Input.GetMouseButtonDown(1))
        {
            if (rb != null)
            {
                rb.AddForce(tempParent.transform.forward * throwForce);
            }

            Soltar();
        }
    }

    public bool TentarIniciarEscaneamento()
    {
        if (foiEscaneado)
        {
            return false;
        }

        // Marca o item como lido para impedir escaneamentos duplicados.
        foiEscaneado = true;

        // Solta o item antes de finalizar a leitura para evitar conflitos com o objeto segurado.
        Soltar();
        return true;
    }

    public Transform ObterRaizProduto()
    {
        // Entrega a raiz completa para sistemas que precisam instanciar ou mover o asset inteiro.
        if (raizProduto == null)
        {
            raizProduto = ResolverRaizProduto();
        }

        return raizProduto;
    }

    public void ConfigurarComoProdutoDePrateleira(Transform raizCompleta)
    {
        // Guarda o lugar original para permitir reposicao depois que o jogador devolver o produto.
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();

        if (raizProduto == null)
        {
            return;
        }

        produtoDePrateleira = true;
        produtoDeEstoque = false;
        produtoDoCaixa = false;
        estaNaPrateleira = true;
        reservadoPorNpc = false;
        chaveProduto = ObterChaveProduto();
        prateleiraOriginal = raizProduto.parent;
        posicaoLocalOriginal = raizProduto.localPosition;
        rotacaoLocalOriginal = raizProduto.localRotation;
        escalaLocalOriginal = raizProduto.localScale;

        if (!produtosDePrateleiraRegistrados.Contains(this))
        {
            // Registra este item como um espaco de prateleira que pode ficar vazio.
            produtosDePrateleiraRegistrados.Add(this);
        }
    }

    public void ConfigurarComoProdutoDeEstoque(Transform raizCompleta)
    {
        // Guarda a raiz completa do item do estoque para mover o asset inteiro.
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();

        if (raizProduto == null)
        {
            return;
        }

        produtoDePrateleira = false;
        produtoDeEstoque = true;
        produtoDoCaixa = false;
        estaNaPrateleira = false;
        reservadoPorNpc = false;
        chaveProduto = ObterChaveProduto();
    }

    public void ConfigurarComoProdutoDoCaixa(Transform raizCompleta)
    {
        // Copias no caixa sao produtos vendidos, nao espacos de prateleira nem estoque.
        produtoCompletoRoot = raizCompleta != null ? raizCompleta : produtoCompletoRoot;
        raizProduto = ResolverRaizProduto();
        produtoDePrateleira = false;
        produtoDeEstoque = false;
        produtoDoCaixa = true;
        estaNaPrateleira = false;
        reservadoPorNpc = false;
        chaveProduto = ObterChaveProduto();
    }

    public bool EstaDisponivelNaPrateleira()
    {
        // O NPC so pode pegar produtos que ainda estejam no lugar da prateleira.
        return produtoDePrateleira &&
               estaNaPrateleira &&
               !reservadoPorNpc &&
               ObterRaizProduto() != null &&
               ObterRaizProduto().gameObject.activeInHierarchy;
    }

    public bool ReservarParaNpc()
    {
        if (!EstaDisponivelNaPrateleira())
        {
            return false;
        }

        // Reserva evita que dois clientes tentem pegar o mesmo item ao mesmo tempo.
        reservadoPorNpc = true;
        return true;
    }

    public void RetirarDaPrateleiraParaNpc()
    {
        Transform raiz = ObterRaizProduto();
        if (raiz == null)
        {
            return;
        }

        // Esconde o produto na hora que o NPC chega nele, deixando a prateleira vazia.
        estaNaPrateleira = false;
        raiz.gameObject.SetActive(false);
    }

    public void LiberarDepoisDoAtendimento()
    {
        // Depois do atendimento, este espaco fica livre para receber reposicao do estoque.
        reservadoPorNpc = false;
    }

    public string ObterMensagemEscaneamento()
    {
        // Monta o texto com as informacoes do produto para mostrar na tela do scanner.
        string nomeExibicao = string.IsNullOrWhiteSpace(nomeProduto) ? gameObject.name : nomeProduto;
        string mensagem = "Produto: " + nomeExibicao;

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
        // Usa o nome configurado no Inspector ou o nome do GameObject como fallback.
        return string.IsNullOrWhiteSpace(nomeProduto) ? gameObject.name : nomeProduto;
    }

    public float ObterPrecoProduto()
    {
        // Entrega o preco para o sistema do caixa calcular o troco.
        return precoProduto;
    }

    public float ObterDuracaoMensagemEscaneamento()
    {
        // Define por quanto tempo a mensagem do produto fica visivel.
        return duracaoMensagemEscaneamento;
    }

    public void FinalizarEscaneamento()
    {
        if (produtoDePrateleira)
        {
            // O produto-base da prateleira fica escondido ate alguem repor pelo estoque.
            LiberarDepoisDoAtendimento();
            return;
        }

        if (produtoDoCaixa || destruirAposEscanear)
        {
            // Remove o produto vendido do caixa depois do escaneamento.
            Transform raiz = ObterRaizProduto();
            Destroy(raiz != null ? raiz.gameObject : gameObject);
        }
    }

    private void Soltar()
    {
        // Solta o item e devolve gravidade para ele.
        if (estaSegurando)
        {
            estaSegurando = false;
            Transform raiz = ObterRaizProduto();
            objPosition = raiz != null ? raiz.position : transform.position;

            if (raiz != null)
            {
                raiz.position = objPosition;
                raiz.SetParent(null);
            }

            if (rb != null)
            {
                rb.useGravity = true;
            }

            TentarReporNaPrateleira();
        }
    }

    private Transform ResolverRaizProduto()
    {
        // Quando uma raiz foi definida no Inspector, ela vence qualquer fallback automatico.
        if (produtoCompletoRoot != null)
        {
            return produtoCompletoRoot;
        }

        // Fallback seguro para objetos simples onde o Pickup ja esta na raiz.
        return transform;
    }

    private Rigidbody ObterRigidbodyDoProduto()
    {
        Transform raiz = ObterRaizProduto();
        if (raiz == null)
        {
            return GetComponent<Rigidbody>();
        }

        // Aceita Rigidbody na raiz ou em algum child do asset importado.
        Rigidbody rigidbodyRaiz = raiz.GetComponent<Rigidbody>();
        return rigidbodyRaiz != null ? rigidbodyRaiz : raiz.GetComponentInChildren<Rigidbody>();
    }

    private void TentarReporNaPrateleira()
    {
        if (produtoDeEstoque)
        {
            // Itens do estoque abastecem uma prateleira vazia equivalente.
            TentarAbastecerPrateleiraComEstoque();
            return;
        }

        if (!produtoDePrateleira || reservadoPorNpc || prateleiraOriginal == null)
        {
            return;
        }

        Transform raiz = ObterRaizProduto();
        if (raiz == null)
        {
            return;
        }

        Vector3 posicaoOriginalMundo = prateleiraOriginal.TransformPoint(posicaoLocalOriginal);
        if (Vector3.Distance(raiz.position, posicaoOriginalMundo) > distanciaParaReporNaPrateleira)
        {
            return;
        }

        // Encaixa de volta no mesmo ponto quando o jogador solta perto da posicao original.
        raiz.SetParent(prateleiraOriginal);
        raiz.localPosition = posicaoLocalOriginal;
        raiz.localRotation = rotacaoLocalOriginal;
        raiz.localScale = escalaLocalOriginal;
        estaNaPrateleira = true;
        foiEscaneado = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void TentarAbastecerPrateleiraComEstoque()
    {
        Transform raizEstoque = ObterRaizProduto();
        if (raizEstoque == null)
        {
            return;
        }

        Pickup melhorEspaco = null;
        float melhorDistancia = float.MaxValue;

        for (int i = produtosDePrateleiraRegistrados.Count - 1; i >= 0; i--)
        {
            Pickup espaco = produtosDePrateleiraRegistrados[i];
            if (espaco == null)
            {
                produtosDePrateleiraRegistrados.RemoveAt(i);
                continue;
            }

            if (!espaco.PodeReceberReposicaoDe(this))
            {
                continue;
            }

            float distancia = Vector3.Distance(raizEstoque.position, espaco.ObterPosicaoOriginalMundo());
            if (distancia < melhorDistancia)
            {
                melhorDistancia = distancia;
                melhorEspaco = espaco;
            }
        }

        if (melhorEspaco == null || melhorDistancia > distanciaParaReporNaPrateleira)
        {
            return;
        }

        // Reativa o produto da prateleira no lugar correto e consome o item do estoque.
        melhorEspaco.ReporPrateleira();
        Destroy(raizEstoque.gameObject);
    }

    private bool PodeReceberReposicaoDe(Pickup produtoEstoque)
    {
        if (!produtoDePrateleira || estaNaPrateleira || reservadoPorNpc || produtoEstoque == null)
        {
            return false;
        }

        return ObterChaveProduto() == produtoEstoque.ObterChaveProduto();
    }

    private void ReporPrateleira()
    {
        Transform raiz = ObterRaizProduto();
        if (raiz == null || prateleiraOriginal == null)
        {
            return;
        }

        // Volta o produto-base para o slot original da prateleira.
        raiz.SetParent(prateleiraOriginal);
        raiz.localPosition = posicaoLocalOriginal;
        raiz.localRotation = rotacaoLocalOriginal;
        raiz.localScale = escalaLocalOriginal;
        raiz.gameObject.SetActive(true);
        estaNaPrateleira = true;
        reservadoPorNpc = false;
        foiEscaneado = false;
    }

    private Vector3 ObterPosicaoOriginalMundo()
    {
        if (prateleiraOriginal == null)
        {
            Transform raiz = ObterRaizProduto();
            return raiz != null ? raiz.position : transform.position;
        }

        return prateleiraOriginal.TransformPoint(posicaoLocalOriginal);
    }

    private string ObterChaveProduto()
    {
        // A chave une estoque e prateleira sem depender de referencia manual no Inspector.
        string textoBase = !string.IsNullOrWhiteSpace(codigoProduto) ? codigoProduto : ObterNomeProduto();
        return NormalizarTexto(textoBase);
    }

    private string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            Transform raiz = ObterRaizProduto();
            texto = raiz != null ? raiz.name : gameObject.name;
        }

        return texto.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);
    }
}
