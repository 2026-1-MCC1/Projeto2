using System.Collections;
using UnityEngine;

public class SistemaCaixa : MonoBehaviour
{
    // Estados do cliente (controla em que fase ele está)
    enum EstadoCliente
    {
        AguardandoProximoCliente, // esperando próximo cliente aparecer
        IndoAoCaixa,              // cliente indo até o caixa
        AguardandoEscaneamento,   // esperando você escanear o item
        AguardandoTroco,          // esperando você entregar o troco
        IndoEmbora                // cliente indo embora
    }

    // Referências principais
    [SerializeField] Scanner scanner;           // scanner do caixa
    [SerializeField] GoalManager goalManager;   // sistema de pontuação
    [SerializeField] Transform jogador;         // player
    [SerializeField] GameObject npcCliente;     // NPC cliente
    [SerializeField] Pickup modeloProduto;      // prefab do produto
    [SerializeField] Transform spawnProduto;

    // caminhos do NPC
    [SerializeField] Transform[] caminhoEntrada; // caminho até o caixa
    [SerializeField] Transform[] caminhoSaida;   // caminho para sair da loja

    int indiceCaminho = 0; // controla em qual ponto do caminho o NPC está

    // Configurações
    [SerializeField] float velocidadeNpc = 2.2f;
    [SerializeField] float tempoEntreClientes = 4f;
    [SerializeField] float distanciaEntregaTroco = 2.5f;
    [SerializeField] KeyCode teclaEntregarTroco = KeyCode.T;

    // Valores possíveis que o cliente pode pagar
    [SerializeField] float[] valoresPagamento = { 20f, 50f, 100f };

    EstadoCliente estadoAtual = EstadoCliente.AguardandoProximoCliente;

    Vector3 posicaoSpawnNpc; // onde o NPC nasce

    Pickup produtoAtual; // produto atual sendo atendido

    float valorPagoAtual; // quanto o cliente pagou
    float trocoAtual;     // troco a ser dado

    bool sistemaPronto; // controla se tudo foi inicializado corretamente

    // Evento: quando um produto é escaneado
    private void OnEnable()
    {
        Scanner.ProdutoEscaneado += AoProdutoEscaneado;
    }

    private void OnDisable()
    {
        Scanner.ProdutoEscaneado -= AoProdutoEscaneado;
    }

    private void Awake()
    {
        // Tenta encontrar referências automaticamente se não estiverem ligadas

        if (scanner == null)
            scanner = Object.FindFirstObjectByType<Scanner>();

        if (goalManager == null)
            goalManager = Object.FindFirstObjectByType<GoalManager>();

        if (jogador == null)
        {
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player != null)
                jogador = player.transform;
        }

        if (npcCliente == null)
            npcCliente = GameObject.Find("NPC1");

        if (modeloProduto == null)
        {
            GameObject obj = GameObject.Find("Box1");
            if (obj != null)
                modeloProduto = obj.GetComponent<Pickup>();
        }


        // Se faltar algo importante, desativa o script
        if (scanner == null || npcCliente == null || modeloProduto == null)
        {
            Debug.LogWarning("Faltando referências!");
            enabled = false;
            return;
        }

        // Guarda posição inicial do NPC (spawn)
        posicaoSpawnNpc = npcCliente.transform.position;

        // Esconde NPC e produto até usar
        npcCliente.SetActive(false);
        modeloProduto.gameObject.SetActive(false);

        // Limpa UI
        scanner.LimparTextoInfoProduto();

        sistemaPronto = true;
    }

    private void Start()
    {
        if (sistemaPronto)
            StartCoroutine(RotinaClientes()); // inicia loop de clientes
    }

    private void Update()
    {
        if (!sistemaPronto) return;

        // Se o NPC estiver indo ao caixa
        if (estadoAtual == EstadoCliente.IndoAoCaixa)
        {
            SeguirCaminho(caminhoEntrada, EstadoCliente.AguardandoEscaneamento, AoChegarNoCaixa);
        }
        // Se o NPC estiver indo embora
        else if (estadoAtual == EstadoCliente.IndoEmbora)
        {
            SeguirCaminho(caminhoSaida, EstadoCliente.AguardandoProximoCliente, AoClienteIrEmbora);
        }
        // Se estiver esperando o troco
        else if (estadoAtual == EstadoCliente.AguardandoTroco)
        {
            // verifica se apertou tecla e está perto do caixa
            if (Input.GetKeyDown(teclaEntregarTroco) && JogadorPertoDoScanner())
            {
                EntregarTroco();
            }
        }
    }

    // Loop infinito de clientes
    private IEnumerator RotinaClientes()
    {
        while (true)
        {
            yield return new WaitForSeconds(tempoEntreClientes); // espera tempo

            IniciarAtendimento(); // inicia cliente

            // espera terminar atendimento
            while (estadoAtual != EstadoCliente.AguardandoProximoCliente)
            {
                yield return null;
            }
        }
    }

    // Inicia atendimento de um cliente
    private void IniciarAtendimento()
    {
        valorPagoAtual = 0f;
        trocoAtual = 0f;
        produtoAtual = null;

        npcCliente.transform.position = posicaoSpawnNpc; // volta para spawn
        npcCliente.SetActive(true); // ativa NPC

        indiceCaminho = 0; // reseta caminho

        estadoAtual = EstadoCliente.IndoAoCaixa;
    }

    // Quando o NPC chega no caixa
    private void AoChegarNoCaixa()
    {
        produtoAtual = SpawnarProdutoDoCliente(); // cria produto

        if (produtoAtual != null)
        {
            scanner.MostrarTextoInfoProduto(
                "Cliente no caixa.\n\n" +
                produtoAtual.ObterMensagemEscaneamento() +
                "\n\nEscaneie o produto.",
                0f
            );
        }
    }

    // Quando o produto é escaneado
    private void AoProdutoEscaneado(Pickup pickup)
    {
        if (estadoAtual != EstadoCliente.AguardandoEscaneamento || pickup != produtoAtual)
            return;

        // calcula pagamento e troco
        valorPagoAtual = SortearValorPago(pickup.ObterPrecoProduto());
        trocoAtual = Mathf.Max(0f, valorPagoAtual - pickup.ObterPrecoProduto());

        estadoAtual = EstadoCliente.AguardandoTroco;

        // mostra info na tela
        scanner.MostrarTextoInfoProduto(
            pickup.ObterMensagemEscaneamento() +
            "\n\nCliente pagou: R$ " + valorPagoAtual.ToString("F2") +
            "\nTroco: R$ " + trocoAtual.ToString("F2") +
            "\nAperte " + teclaEntregarTroco,
            0f
        );
    }

    // Entrega o troco
    private void EntregarTroco()
    {
        if (goalManager != null)
        {
            goalManager.ClienteAtendido();
            goalManager.MostrarMensagem("Troco entregue", 3f);
        }

        scanner.MostrarTextoInfoProduto("Cliente atendido!", 2.5f);

        indiceCaminho = 0; // reseta caminho de saída
        estadoAtual = EstadoCliente.IndoEmbora;
    }

    // Quando o cliente vai embora
    private void AoClienteIrEmbora()
    {
        npcCliente.SetActive(false); // desativa NPC
        estadoAtual = EstadoCliente.AguardandoProximoCliente;
    }

    // 🔥 SISTEMA DE MOVIMENTO POR CAMINHO
    void SeguirCaminho(Transform[] caminho, EstadoCliente proximoEstado, System.Action aoFinal)
    {
        if (caminho == null || caminho.Length == 0) return;

        Transform destino = caminho[indiceCaminho]; // ponto atual

        Vector3 posAtual = npcCliente.transform.position;

        // ignora altura (movimento no chão)
        Vector3 destinoPlano = new Vector3(destino.position.x, posAtual.y, destino.position.z);

        // move o NPC
        npcCliente.transform.position = Vector3.MoveTowards(
            posAtual,
            destinoPlano,
            velocidadeNpc * Time.deltaTime
        );

        // gira o NPC para a direção
        Vector3 direcao = destinoPlano - posAtual;
        if (direcao.sqrMagnitude > 0.001f)
        {
            npcCliente.transform.rotation = Quaternion.LookRotation(direcao.normalized);
        }

        // chegou no ponto
        if (Vector3.Distance(posAtual, destinoPlano) < 0.1f)
        {
            indiceCaminho++; // vai pro próximo ponto

            // terminou o caminho
            if (indiceCaminho >= caminho.Length)
            {
                indiceCaminho = 0;
                estadoAtual = proximoEstado;
                aoFinal?.Invoke(); // executa ação final
            }
        }
    }

    // Cria produto na frente do caixa
    private Pickup SpawnarProdutoDoCliente()
    {
        // verifica se o ponto de spawn foi definido
        if (spawnProduto == null)
        {
            Debug.LogError("SpawnProduto não definido!");
            return null;
        }

        // cria o produto na posição do spawn
        Pickup novoProduto = Instantiate(
            modeloProduto,
            spawnProduto.position,
            spawnProduto.rotation
        );

        // garante que o objeto está ativo
        novoProduto.gameObject.SetActive(true);

        return novoProduto;
    }

    // Verifica se jogador está perto do caixa
    private bool JogadorPertoDoScanner()
    {
        if (jogador == null) return true;

        return Vector3.Distance(jogador.position, scanner.transform.position) <= distanciaEntregaTroco;
    }

    // Sorteia valor pago maior que o preço
    private float SortearValorPago(float preco)
    {
        foreach (float v in valoresPagamento)
        {
            if (v > preco)
                return v;
        }
        return preco + 10f;
    }
}