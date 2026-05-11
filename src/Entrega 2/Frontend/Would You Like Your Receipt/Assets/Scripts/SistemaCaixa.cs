using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Serialization;

[System.Serializable]
public class ConfiguracaoClienteCaixa
{
    // Nome livre para identificar esta variacao no Inspector.
    public string nomeVariacao = "Cliente";
    // Modelo do NPC usado nesta variacao.
    public GameObject npc;
    // Produto que este NPC leva para o caixa.
    public Pickup produto;
    // Caminho proprio de entrada deste NPC, caso voce queira uma rota fixa para ele.
    public Transform[] caminhoEntrada;
    // Caminho proprio de saida deste NPC, caso voce queira uma rota fixa para ele.
    public Transform[] caminhoSaida;
}

[System.Serializable]
public class RotaWaypoint
{
    // Nome da rota para ficar facil de ler no Inspector.
    public string nomeRota = "Rota";
    // Lista ordenada dos pontos por onde o NPC vai passar.
    public Transform[] pontos;
}

public class SistemaCaixa : MonoBehaviour
{
    // Este e o sistema principal dos clientes automaticos.
    // Ele escolhe NPCs, produtos, rotas, controla a ida ao produto, ida ao caixa, scan, troco e saida.
    // A maior parte do comportamento do mercado acontece aqui.
    // Estados principais do fluxo de atendimento.
    enum EstadoCliente
    {
        AguardandoProximoCliente,
        IndoAoProduto,
        IndoAoCaixa,
        AguardandoEscaneamento,
        AguardandoTroco,
        IndoEmbora
    }

    // Referencia ao scanner do caixa.
    [SerializeField] Scanner scanner;
    // Referencia ao GoalManager para contar objetivos.
    [SerializeField] GoalManager goalManager;
    // Referencia ao jogador para validar a entrega do troco.
    [SerializeField] Transform jogador;

    // NPC principal legado da cena.
    [SerializeField] GameObject npcCliente;
    // Produto principal legado da cena.
    [SerializeField] Pickup modeloProduto;
    // Ponto onde o item aparece no caixa.
    [SerializeField] Transform spawnProduto;

    // Listas extras para adicionar mais NPCs e produtos no Inspector.
    [SerializeField] GameObject[] npcsExtras;
    [SerializeField] Pickup[] produtosExtras;
    // Configuracoes prontas para parear NPC + produto + rota fixa.
    [SerializeField] ConfiguracaoClienteCaixa[] configuracoesClientes = new ConfiguracaoClienteCaixa[8];

    // Rotas aleatorias de entrada que podem ser sorteadas quando o NPC nao tiver rota propria.
    [SerializeField] RotaWaypoint[] rotasEntradaAleatorias;
    // Rotas aleatorias de saida que podem ser sorteadas quando o NPC nao tiver rota propria.
    [SerializeField] RotaWaypoint[] rotasSaidaAleatorias;

    // Fallback antigo de waypoints, caso voce prefira uma lista unica.
    [SerializeField] Transform[] caminhoEntrada;
    // Fallback antigo de waypoints para a saida.
    [SerializeField] Transform[] caminhoSaida;
    // Pontos possiveis de spawn do cliente.
    [SerializeField] Transform[] pontosSpawn;
    // Ponto principal de spawn do cliente.
    [SerializeField] Transform spawnNPC;
    // Ponto final onde o cliente some.
    [SerializeField] Transform despawnNPC;
    // Ponto manual onde ele deve parar em frente ao caixa.
    [SerializeField] Transform pontoParadaCaixa;
    // Raiz onde ficam os produtos reais e interagiveis usados como molde do item que nasce no caixa.
    [FormerlySerializedAs("raizInteractables")]
    [SerializeField] Transform raizProdutosInteragiveis;
    // Raiz onde ficam os produtos apenas visuais da loja, que o NPC visita na prateleira.
    [SerializeField] Transform raizProdutosVisuaisLoja;
    // Raiz opcional que delimita quais produtos visuais realmente podem ser escolhidos pelo NPC.
    [SerializeField] Transform raizProdutosAtendimentoNpc;
    // Superficie de navegacao usada para o NPC respeitar as colisoes da loja.
    [SerializeField] NavMeshSurface superficieNavMesh;
    // Raiz usada para limitar a NavMesh so a area interna do mercado.
    [SerializeField] Transform raizAreaNavMesh;

    // Velocidade de deslocamento do cliente.
    [SerializeField] float velocidadeNpc = 2.2f;
    // Tempo para o NPC ganhar velocidade de forma mais natural.
    [SerializeField] float aceleracaoNpc = 5.5f;
    // Tempo para o NPC frear de forma mais natural perto dos pontos.
    [SerializeField] float desaceleracaoNpc = 7.5f;
    // Suaviza a rotacao do corpo do NPC quando ele muda de direcao.
    [SerializeField] float velocidadeRotacaoNpc = 7f;
    // Distancia em que o NPC comeca a reduzir a velocidade antes de parar.
    [SerializeField] float distanciaFreioNpc = 1.1f;
    // Faz o NPC diminuir um pouco a velocidade quando a curva e muito fechada.
    [SerializeField] float velocidadeMinimaEmCurva = 0.55f;
    // Tempo entre um cliente e outro.
    [SerializeField] float tempoEntreClientes = 4f;
    // Distancia horizontal maxima para o jogador confirmar o troco.
    [SerializeField] float distanciaEntregaTroco = 2.5f;
    // Distancia usada para considerar que o NPC chegou ao waypoint.
    [SerializeField] float distanciaChegadaWaypoint = 0.12f;
    // Distancia minima entre o scanner e o item que nasce no caixa.
    [SerializeField] float distanciaMinimaSpawnProdutoDoScanner = 1.05f;
    // Altura aproximada em que o cliente segura o produto durante a compra.
    [SerializeField] Vector3 offsetProdutoCarregadoNpc = new Vector3(0f, 0.48f, 0.42f);
    // Escala visual aplicada ao produto carregado para ele nao atrapalhar o corpo do NPC.
    [SerializeField] float escalaProdutoCarregadoNpc = 0.35f;
    // Tamanho visual maximo do produto enquanto esta na mao do NPC.
    [SerializeField] float tamanhoMaximoProdutoCarregadoNpc = 0.45f;
    // Tempo de protecao para o item nao ser lido pelo scanner no mesmo frame.
    [SerializeField] float tempoIgnorarScannerAoSpawnar = 0.75f;
    // Diz se o fallback antigo pode embaralhar os pontos de entrada.
    [SerializeField] bool sortearCaminhoEntrada = true;
    // Diz se o fallback antigo pode embaralhar os pontos de saida.
    [SerializeField] bool sortearCaminhoSaida = true;
    // Valores que o cliente pode usar para pagar.
    [SerializeField] float[] valoresPagamento = { 20f, 50f, 100f };
    // Evita repetir o mesmo NPC duas vezes seguidas quando possivel.
    [SerializeField] bool evitarRepetirNpcEmSequencia = true;
    // Limite maximo de digitos aceitos na digitacao do troco.
    [SerializeField] int maximoDigitosTroco = 6;
    // Liga a montagem automatica de caminho ate um produto da loja antes do caixa.
    [SerializeField] bool gerarCaminhoAutomaticoProdutos = true;
    // Quantidade minima de waypoints escolhidos para ir ate o produto.
    [SerializeField] int minimoPontosAteProduto = 1;
    // Quantidade maxima de waypoints escolhidos para ir ate o produto.
    [SerializeField] int maximoPontosAteProduto = 3;
    // Quantidade minima de waypoints escolhidos do produto ate o caixa.
    [SerializeField] int minimoPontosAteCaixa = 0;
    // Quantidade maxima de waypoints escolhidos do produto ate o caixa.
    [SerializeField] int maximoPontosAteCaixa = 2;
    // Quantos pontos aleatorios o cliente pode visitar antes de ir ao produto.
    [SerializeField] int maximoPontosPasseioAntesProduto = 2;
    // Distancia que o NPC deve manter do produto visual para parar na frente dele.
    [SerializeField] float distanciaParadaNoProduto = 1.1f;
    // Diferenca minima de distancia para considerar que um waypoint realmente aproxima o NPC do destino.
    [SerializeField] float toleranciaMelhoraWaypoint = 0.15f;
    // Raio aproximado do corpo do NPC para detectar obstaculos ao andar.
    [SerializeField] float raioColisaoNpc = 0.35f;
    // Altura aproximada do corpo do NPC para detectar obstaculos ao andar.
    [SerializeField] float alturaColisaoNpc = 1.8f;
    // Pequena folga extra para parar antes de encostar no obstaculo.
    [SerializeField] float margemColisaoNpc = 0.05f;
    // Camadas que o NPC deve respeitar ao caminhar pela loja.
    [SerializeField] LayerMask mascaraColisaoNpc = ~0;
    // Liga o uso de NavMeshAgent para o NPC navegar pela loja.
    [SerializeField] bool usarNavMesh = true;
    // Decide se a NavMesh deve ser montada no Play ou se o sistema vai usar a malha ja assada no editor.
    [SerializeField] bool construirNavMeshEmRuntime = false;
    // Limita a NavMesh a um volume do mercado para o NPC nao tentar navegar pela rua.
    [SerializeField] bool limitarNavMeshAoMercado = true;
    // Distancia maxima para procurar uma posicao valida sobre o NavMesh.
    [SerializeField] float raioAmostraNavMesh = 2.5f;
    // Distancia usada para considerar que o agent chegou ao destino atual.
    [SerializeField] float distanciaChegadaNavMesh = 0.2f;
    // Aceleracao do NavMeshAgent.
    [SerializeField] float aceleracaoNavMesh = 14f;
    // Velocidade angular do NavMeshAgent para curvas mais naturais.
    [SerializeField] float velocidadeAngularNavMesh = 540f;
    // Margem extra aplicada ao volume da NavMesh para nao cortar corredor, entrada e caixa por muito pouco.
    [SerializeField] Vector3 margemAreaNavMesh = new Vector3(1.5f, 2f, 1.5f);
    // Limita os produtos do NPC aos mais proximos do caixa para evitar sorteio no fundo da loja.
    [SerializeField] bool limitarProdutosPelaDistanciaAoCaixa = true;
    // Distancia maxima do produto visual ate o caixa para ele entrar no sorteio do NPC.
    [SerializeField] float distanciaMaximaProdutoDoCaixa = 9f;
    // Quantos produtos mais proximos do caixa entram no sorteio quando houver muitos na loja.
    [SerializeField] int quantidadeProdutosMaisProximos = 12;

    // Modelos de NPC disponiveis para atendimento.
    readonly List<GameObject> modelosNpc = new List<GameObject>();
    // Modelos de produto disponiveis para atendimento.
    readonly List<Pickup> modelosProduto = new List<Pickup>();
    // Produtos reais da loja usados como destino visual para o cliente.
    readonly List<Pickup> produtosDaLoja = new List<Pickup>();
    // Pool de pontos que o sistema pode usar para montar caminhos automaticamente.
    readonly List<Transform> pontosCaminhoAutomatico = new List<Transform>();
    // Caminho temporario do spawn ate um produto da loja.
    readonly List<Transform> caminhoProdutoAtual = new List<Transform>();
    // Rota de entrada do cliente atual.
    readonly List<Transform> caminhoEntradaAtual = new List<Transform>();
    // Rota de saida do cliente atual.
    readonly List<Transform> caminhoSaidaAtual = new List<Transform>();
    // Indices das configuracoes realmente validas.
    readonly List<int> indicesConfiguracoesValidas = new List<int>();

    // Estado atual do sistema.
    EstadoCliente estadoAtual = EstadoCliente.AguardandoProximoCliente;

    // Referencias do atendimento atual.
    GameObject npcAtual;
    NavMeshAgent agenteNpcAtual;
    Pickup produtoAtual;
    Pickup produtoDestinoAtual;
    Pickup modeloProdutoAtual;
    Pickup produtoCarregadoAtual;
    GameObject objetoProdutoCarregadoAtual;
    Vector3 escalaOriginalProdutoCarregadoAtual = Vector3.one;
    ConfiguracaoClienteCaixa configuracaoAtual;

    // Indices dos waypoints em andamento.
    int indiceWaypointProdutoAtual;
    int indiceWaypointEntradaAtual;
    int indiceWaypointSaidaAtual;
    // Indices auxiliares para evitar repeticao.
    int ultimoIndiceNpc = -1;
    int ultimoIndiceConfiguracao = -1;
    int ultimoIndiceRotaEntrada = -1;
    int ultimoIndiceRotaSaida = -1;

    // Valores financeiros do atendimento atual.
    float totalCompra;
    float valorPago;
    float troco;

    // Controle geral para saber se o sistema conseguiu subir.
    bool sistemaPronto;
    // Guarda a velocidade atual do NPC para acelerar e frear de forma mais suave.
    float velocidadeNpcAtual;
    // Ultima direcao valida usada para orientar o corpo do NPC.
    Vector3 ultimaDirecaoMovimentoNpc = Vector3.forward;
    // Guarda o ponto exato onde o NPC deve parar ao chegar ao produto visual da loja.
    Vector3 posicaoParadaProdutoAtual;
    // Indica se o ponto de parada do produto foi calculado para o atendimento atual.
    bool possuiPosicaoParadaProdutoAtual;
    // Guarda o ultimo destino enviado para o NavMeshAgent para evitar SetDestination a cada frame.
    Vector3 ultimoDestinoNavMesh;
    // Diz se o ultimo destino ja foi enviado ao agent durante o atendimento atual.
    bool destinoNavMeshDefinido;
    // Posicao usada para detectar quando o NavMeshAgent ficou preso.
    Vector3 ultimaPosicaoAgenteNavMesh;
    // Tempo acumulado sem progresso real do agent.
    float tempoAgenteSemProgresso;
    // Marca se a NavMesh ficou pronta para uso neste Play.
    bool navMeshPronto;
    // Texto digitado em centavos para a etapa do troco.
    string trocoDigitadoEmCentavos = string.Empty;
    // Mensagem base mostrada ao jogador enquanto o troco esta sendo digitado.
    string mensagemTrocoAtual = string.Empty;
    // Aviso de erro ou orientacao complementar.
    string avisoTrocoAtual = string.Empty;

    private void OnValidate()
    {
        // Mantem oito espacos prontos no Inspector para facilitar a montagem do projeto.
        if (configuracoesClientes == null || configuracoesClientes.Length < 8)
        {
            System.Array.Resize(ref configuracoesClientes, 8);
        }
    }

    private void OnEnable()
    {
        // Escuta o evento do scanner para reagir quando o item correto passar por ele.
        Scanner.ProdutoEscaneado += AoProdutoEscaneado;
    }

    private void OnDisable()
    {
        // Remove a inscricao para nao deixar evento pendurado em objeto destruido.
        Scanner.ProdutoEscaneado -= AoProdutoEscaneado;
    }

    private void Awake()
    {
        // Awake prepara referencias e listas antes dos clientes comecarem a spawnar.
        // Se algo essencial nao existir, o sistema desliga para evitar erro em loop.
        if (scanner == null)
        {
            scanner = Object.FindFirstObjectByType<Scanner>();
        }

        if (goalManager == null)
        {
            goalManager = GoalManager.Instance;
            if (goalManager == null)
            {
                goalManager = Object.FindFirstObjectByType<GoalManager>();
            }
        }

        if (jogador == null)
        {
            PlayerMovement playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
            {
                jogador = playerMovement.transform;
            }
        }

        // Resolve automaticamente os pontos principais da cena pelo nome.
        PrepararPontosDaCena();
        // Garante que o array de configuracoes sempre exista em runtime.
        PrepararConfiguracoesClientes();
        // Monta as listas de NPCs e produtos usando os campos atuais da cena.
        PrepararModelosNpc();
        PrepararModelosProduto();
        PrepararProdutosDaLoja();
        PrepararPontosCaminhoAutomatico();
        PrepararIndicesConfiguracoesValidas();

        if (scanner == null || ObterPontoSpawnCliente() == null || despawnNPC == null || spawnProduto == null)
        {
            Debug.LogWarning("SistemaCaixa precisa de Scanner, SpawnNPC, DespawnNPC e SpawnObjects.", this);
            enabled = false;
            return;
        }

        if (indicesConfiguracoesValidas.Count == 0 && (modelosNpc.Count == 0 || (modelosProduto.Count == 0 && produtosDaLoja.Count == 0)))
        {
            Debug.LogWarning("SistemaCaixa precisa de pelo menos um NPC e um produto configurados.", this);
            enabled = false;
            return;
        }

        scanner.LimparTextoInfoProduto();
        sistemaPronto = true;
    }

    private void Start()
    {
        // Start prepara a NavMesh e inicia a rotina infinita de clientes.
        if (sistemaPronto)
        {
            // Monta a NavMesh em runtime usando as colisoes atuais da cena sem apagar nada do usuario.
            PrepararNavMeshDaLoja();
            StartCoroutine(RotinaClientes());
        }
    }

    private void Update()
    {
        // Update move o cliente conforme o estado atual do atendimento.
        // Cada estado representa uma etapa: ir ao produto, ir ao caixa, aguardar scan, troco ou ir embora.
        if (!sistemaPronto)
        {
            return;
        }

        if (estadoAtual == EstadoCliente.IndoAoProduto)
        {
            // Faz o cliente seguir primeiro um caminho automatico ate o produto escolhido.
            MoverNpcPeloCaminho(
                caminhoProdutoAtual,
                ref indiceWaypointProdutoAtual,
                ObterPosicaoProdutoDestinoAtual(),
                EstadoCliente.IndoAoCaixa,
                AoChegarNoProduto
            );
        }
        else if (estadoAtual == EstadoCliente.IndoAoCaixa)
        {
            // Faz o cliente seguir a rota escolhida ate parar no ponto do caixa.
            MoverNpcPeloCaminho(
                caminhoEntradaAtual,
                ref indiceWaypointEntradaAtual,
                CalcularPontoParadaNoCaixa(),
                EstadoCliente.AguardandoEscaneamento,
                AoChegarNoCaixa
            );
        }
        else if (estadoAtual == EstadoCliente.IndoEmbora)
        {
            // Faz o cliente seguir a rota de saida ate o ponto de sumir.
            MoverNpcPeloCaminho(
                caminhoSaidaAtual,
                ref indiceWaypointSaidaAtual,
                despawnNPC.position,
                EstadoCliente.AguardandoProximoCliente,
                AoClienteIrEmbora
            );
        }
        else if (estadoAtual == EstadoCliente.AguardandoTroco)
        {
            // Permite digitar o troco diretamente no teclado.
            ProcessarDigitacaoTroco();
        }
    }

    IEnumerator RotinaClientes()
    {
        // Coroutine que cria clientes continuamente enquanto o caixa estiver ativo.
        // Loop principal que cria novos clientes ao longo da partida.
        while (true)
        {
            if (estadoAtual == EstadoCliente.AguardandoProximoCliente)
            {
                yield return new WaitForSeconds(tempoEntreClientes);

                if (estadoAtual == EstadoCliente.AguardandoProximoCliente)
                {
                    IniciarAtendimento();
                }
            }

            yield return null;
        }
    }

    void IniciarAtendimento()
    {
        // Comeca um novo atendimento: escolhe NPC, produto, rotas e instancia o cliente.
        if (estadoAtual != EstadoCliente.AguardandoProximoCliente)
        {
            return;
        }

        // Limpa qualquer sobra do atendimento anterior antes de criar o proximo.
        LimparAtendimentoAtual();

        GameObject modeloNpcEscolhido = null;
        bool usandoCaminhoAutomaticoParaProduto = false;
        modeloProdutoAtual = null;
        produtoDestinoAtual = null;
        configuracaoAtual = null;

        if (TentarEscolherConfiguracaoCliente())
        {
            // Usa a configuracao pronta quando ela estiver preenchida no Inspector.
            modeloNpcEscolhido = configuracaoAtual.npc;
            modeloProdutoAtual = produtosDaLoja.Count > 0 ? SortearProdutoDaLoja() : configuracaoAtual.produto;
            PrepararRotaSaidaAtual(configuracaoAtual);
        }
        else
        {
            // Fallback antigo: sorteia um NPC e pareia o produto pela ordem da lista.
            int indiceNpcEscolhido = SortearIndiceNpc();
            modeloNpcEscolhido = modelosNpc[indiceNpcEscolhido];
            modeloProdutoAtual = ObterProdutoDoNpc(indiceNpcEscolhido);
            ultimoIndiceNpc = indiceNpcEscolhido;
            PrepararRotaSaidaAtual(null);
        }

        if (modeloProdutoAtual == null && produtosDaLoja.Count > 0)
        {
            // Se nao houver modelo configurado, usa um produto real da loja como fallback.
            modeloProdutoAtual = SortearProdutoDaLoja();
        }

        if (gerarCaminhoAutomaticoProdutos && (configuracaoAtual == null || configuracaoAtual.caminhoEntrada == null || configuracaoAtual.caminhoEntrada.Length == 0))
        {
            // Tenta montar o caminho ate o produto e depois ate o caixa sem configuracao manual.
            usandoCaminhoAutomaticoParaProduto = TentarPrepararCaminhosAutomaticosParaProduto();
        }

        if (!usandoCaminhoAutomaticoParaProduto && produtosDaLoja.Count > 0)
        {
            // Mesmo sem rota automatica perfeita, o cliente ainda deve tentar comprar um produto antes do caixa.
            produtoDestinoAtual = ObterProdutoDaLojaCorrespondente(modeloProdutoAtual);
            if (produtoDestinoAtual != null)
            {
                Transform pontoSpawnFallback = ObterPontoSpawnCliente();
                Vector3 referenciaProduto = pontoSpawnFallback != null ? pontoSpawnFallback.position : produtoDestinoAtual.transform.position;
                posicaoParadaProdutoAtual = CalcularPontoParadaNoProduto(produtoDestinoAtual, referenciaProduto);
                possuiPosicaoParadaProdutoAtual = true;
                caminhoProdutoAtual.Clear();
                InserirPasseioAleatorioAntesDoProduto(referenciaProduto, posicaoParadaProdutoAtual, caminhoProdutoAtual);
                caminhoEntradaAtual.Clear();
                usandoCaminhoAutomaticoParaProduto = true;
            }
        }

        if (!usandoCaminhoAutomaticoParaProduto)
        {
            PrepararRotaEntradaAtual(configuracaoAtual);
        }

        // Remove pontos proibidos caso tenham sido arrastados por engano para dentro das rotas.
        RemoverPontoDoCaminho(caminhoProdutoAtual, spawnNPC);
        RemoverPontoDoCaminho(caminhoProdutoAtual, despawnNPC);
        RemoverPontoDoCaminho(caminhoEntradaAtual, spawnNPC);
        RemoverPontoDoCaminho(caminhoEntradaAtual, despawnNPC);
        RemoverPontoDoCaminho(caminhoSaidaAtual, spawnNPC);

        indiceWaypointProdutoAtual = 0;
        indiceWaypointEntradaAtual = 0;
        indiceWaypointSaidaAtual = 0;

        if (modeloNpcEscolhido == null || modeloProdutoAtual == null)
        {
            Debug.LogWarning("SistemaCaixa nao conseguiu escolher um NPC ou um produto valido.", this);
            estadoAtual = EstadoCliente.AguardandoProximoCliente;
            return;
        }

        // Zera os dados financeiros antes de começar o atendimento.
        totalCompra = 0f;
        valorPago = 0f;
        troco = 0f;
        trocoDigitadoEmCentavos = string.Empty;
        mensagemTrocoAtual = string.Empty;
        avisoTrocoAtual = string.Empty;

        // Instancia o clone do cliente no ponto de spawn escolhido.
        Transform pontoSpawn = ObterPontoSpawnCliente();
        npcAtual = Instantiate(modeloNpcEscolhido, pontoSpawn.position, pontoSpawn.rotation);
        npcAtual.name = modeloNpcEscolhido.name;
        npcAtual.SetActive(true);
        ultimaDirecaoMovimentoNpc = npcAtual.transform.forward.sqrMagnitude > 0.0001f ? npcAtual.transform.forward : Vector3.forward;
        velocidadeNpcAtual = 0f;

        // Ajusta o clone para ele obedecer apenas ao sistema do caixa.
        PrepararNpcClonado(npcAtual);

        estadoAtual = usandoCaminhoAutomaticoParaProduto ? EstadoCliente.IndoAoProduto : EstadoCliente.IndoAoCaixa;
    }

    void PrepararNpcClonado(GameObject npcClonado)
    {
        // Prepara o clone do NPC para ser controlado pelo sistema do caixa.
        // O visual/collider original fica, mas a fisica e controlada para evitar tombos.
        if (npcClonado == null)
        {
            return;
        }

        NPCInteraction interacao = npcClonado.GetComponent<NPCInteraction>();
        if (interacao != null)
        {
            // Desliga a conversa do clone para nao conflitar com o fluxo automatico do caixa.
            interacao.enabled = false;
        }

        Rigidbody[] rigidbodies = npcClonado.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            // Zera a fisica do clone para ele nao sair sambando pela cena.
            if (!rigidbodies[i].isKinematic)
            {
                rigidbodies[i].linearVelocity = Vector3.zero;
                rigidbodies[i].angularVelocity = Vector3.zero;
            }

            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }

        Collider[] colliders = npcClonado.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            // Garante que os colliders do modelo estejam ligados para o cast respeitar o proprio volume.
            colliders[i].enabled = true;
        }

        // Liga o clone ao NavMeshAgent para ele navegar pela loja de forma mais robusta.
        ConfigurarAgenteNpc(npcClonado);
    }

    void MoverNpcPeloCaminho(List<Transform> caminhoAtual, ref int indiceWaypointAtual, Vector3 destinoFinal, EstadoCliente proximoEstado, System.Action aoChegar)
    {
        // Move o NPC por uma lista de waypoints e depois para um destino final.
        // Primeiro tenta usar NavMesh; se nao der, usa o fallback manual.
        if (npcAtual == null)
        {
            estadoAtual = EstadoCliente.AguardandoProximoCliente;
            return;
        }

        if (NpcChegouPertoDoProdutoAtual())
        {
            estadoAtual = proximoEstado;
            aoChegar?.Invoke();
            return;
        }

        if (MoverNpcPeloCaminhoComNavMesh(caminhoAtual, ref indiceWaypointAtual, destinoFinal, proximoEstado, aoChegar))
        {
            return;
        }

        // Ignora referencias nulas para nao travar a rota se algum waypoint for apagado.
        while (indiceWaypointAtual < caminhoAtual.Count && caminhoAtual[indiceWaypointAtual] == null)
        {
            indiceWaypointAtual++;
        }

        bool seguindoWaypoint = indiceWaypointAtual < caminhoAtual.Count;
        Vector3 destinoAtual = seguindoWaypoint ? caminhoAtual[indiceWaypointAtual].position : destinoFinal;

        if (!MoverNpcAte(destinoAtual, distanciaChegadaWaypoint))
        {
            return;
        }

        if (seguindoWaypoint)
        {
            // Quando chega ao waypoint atual, passa para o proximo no frame seguinte.
            indiceWaypointAtual++;
            return;
        }

        estadoAtual = proximoEstado;
        aoChegar?.Invoke();
    }

    bool MoverNpcPeloCaminhoComNavMesh(List<Transform> caminhoAtual, ref int indiceWaypointAtual, Vector3 destinoFinal, EstadoCliente proximoEstado, System.Action aoChegar)
    {
        // Versao com NavMeshAgent, usada para o NPC navegar respeitando o bake da loja.
        if (!usarNavMesh || !navMeshPronto || agenteNpcAtual == null || !agenteNpcAtual.enabled || !agenteNpcAtual.isOnNavMesh)
        {
            return false;
        }

        // Ignora referencias nulas para nao travar a rota se algum waypoint for apagado.
        while (indiceWaypointAtual < caminhoAtual.Count && caminhoAtual[indiceWaypointAtual] == null)
        {
            indiceWaypointAtual++;
        }

        bool seguindoWaypoint = indiceWaypointAtual < caminhoAtual.Count;
        Vector3 destinoAtual = seguindoWaypoint ? caminhoAtual[indiceWaypointAtual].position : destinoFinal;

        if (!seguindoWaypoint && NpcChegouPertoDoProdutoAtual())
        {
            estadoAtual = proximoEstado;
            aoChegar?.Invoke();
            return true;
        }

        if (!TentarProjetarNoNavMesh(destinoAtual, out Vector3 destinoNavMesh))
        {
            if (seguindoWaypoint)
            {
                // Se um waypoint cair fora da malha, pula para o proximo em vez de congelar o NPC.
                indiceWaypointAtual++;
                destinoNavMeshDefinido = false;
                return true;
            }

            if (estadoAtual == EstadoCliente.IndoAoProduto)
            {
                AvancarFluxoAposDestinoInacessivel(proximoEstado, aoChegar);
                return true;
            }

            DesativarAgenteNavMeshParaFallback();
            return false;
        }

        DefinirDestinoNpcNoNavMesh(destinoNavMesh);

        if (agenteNpcAtual.pathPending)
        {
            return true;
        }

        if (agenteNpcAtual.pathStatus != NavMeshPathStatus.PathComplete)
        {
            if (seguindoWaypoint)
            {
                ReiniciarDestinoNavMesh();
                indiceWaypointAtual++;
                return true;
            }

            if (estadoAtual == EstadoCliente.IndoAoProduto)
            {
                AvancarFluxoAposDestinoInacessivel(proximoEstado, aoChegar);
                return true;
            }

            DesativarAgenteNavMeshParaFallback();
            return false;
        }

        if (AgenteNpcFicouPreso())
        {
            if (seguindoWaypoint)
            {
                ReiniciarDestinoNavMesh();
                indiceWaypointAtual++;
                return true;
            }

            if (estadoAtual == EstadoCliente.IndoAoProduto)
            {
                AvancarFluxoAposDestinoInacessivel(proximoEstado, aoChegar);
                return true;
            }

            DesativarAgenteNavMeshParaFallback();
            return false;
        }

        if (!AgenteNpcChegouAoDestino())
        {
            return true;
        }

        ReiniciarDestinoNavMesh();

        if (seguindoWaypoint)
        {
            // Quando chega ao waypoint atual, avanca para o proximo.
            indiceWaypointAtual++;
            return true;
        }

        estadoAtual = proximoEstado;
        aoChegar?.Invoke();
        return true;
    }

    bool NpcChegouPertoDoProdutoAtual()
    {
        // Evita o NPC ficar travado tentando encostar exatamente no produto.
        // Se ele chegou perto o bastante, considera que pegou o item.
        if (estadoAtual != EstadoCliente.IndoAoProduto || npcAtual == null || produtoDestinoAtual == null)
        {
            return false;
        }

        Transform raizProduto = produtoDestinoAtual.ObterRaizProduto();
        Vector3 posicaoProduto = raizProduto != null ? raizProduto.position : produtoDestinoAtual.transform.position;
        float distanciaAceitavel = Mathf.Max(distanciaParadaNoProduto + raioColisaoNpc + 0.75f, 1.75f);
        return DistanciaPlana(npcAtual.transform.position, posicaoProduto) <= distanciaAceitavel;
    }

    void DefinirDestinoNpcNoNavMesh(Vector3 destinoNavMesh)
    {
        if (agenteNpcAtual == null || !agenteNpcAtual.enabled || !agenteNpcAtual.isOnNavMesh)
        {
            return;
        }

        if (!destinoNavMeshDefinido || DistanciaPlana(ultimoDestinoNavMesh, destinoNavMesh) > 0.05f || !agenteNpcAtual.hasPath)
        {
            // Evita SetDestination desnecessario a cada frame e deixa o agent trabalhar com calma.
            agenteNpcAtual.SetDestination(destinoNavMesh);
            ultimoDestinoNavMesh = destinoNavMesh;
            destinoNavMeshDefinido = true;
            ultimaPosicaoAgenteNavMesh = agenteNpcAtual.transform.position;
            tempoAgenteSemProgresso = 0f;
        }
    }

    void ReiniciarDestinoNavMesh()
    {
        if (agenteNpcAtual != null && agenteNpcAtual.enabled && agenteNpcAtual.isOnNavMesh)
        {
            agenteNpcAtual.ResetPath();
        }

        destinoNavMeshDefinido = false;
        tempoAgenteSemProgresso = 0f;
    }

    void DesativarAgenteNavMeshParaFallback()
    {
        ReiniciarDestinoNavMesh();

        if (agenteNpcAtual != null)
        {
            agenteNpcAtual.enabled = false;
        }
    }

    bool AgenteNpcFicouPreso()
    {
        if (agenteNpcAtual == null || !agenteNpcAtual.enabled || !agenteNpcAtual.isOnNavMesh || !agenteNpcAtual.hasPath)
        {
            tempoAgenteSemProgresso = 0f;
            return false;
        }

        float deslocamento = DistanciaPlana(agenteNpcAtual.transform.position, ultimaPosicaoAgenteNavMesh);
        if (deslocamento > 0.03f || agenteNpcAtual.velocity.sqrMagnitude > 0.0025f)
        {
            ultimaPosicaoAgenteNavMesh = agenteNpcAtual.transform.position;
            tempoAgenteSemProgresso = 0f;
            return false;
        }

        tempoAgenteSemProgresso += Time.deltaTime;
        return tempoAgenteSemProgresso >= 2.5f;
    }

    void AvancarFluxoAposDestinoInacessivel(EstadoCliente proximoEstado, System.Action aoChegar)
    {
        ReiniciarDestinoNavMesh();

        if (estadoAtual == EstadoCliente.IndoAoProduto)
        {
            produtoDestinoAtual = null;
        }

        estadoAtual = proximoEstado;
        aoChegar?.Invoke();
    }

    bool AgenteNpcChegouAoDestino()
    {
        if (agenteNpcAtual == null || !agenteNpcAtual.enabled || !agenteNpcAtual.isOnNavMesh)
        {
            return false;
        }

        if (agenteNpcAtual.pathPending)
        {
            return false;
        }

        if (agenteNpcAtual.remainingDistance > Mathf.Max(distanciaChegadaNavMesh, agenteNpcAtual.stoppingDistance))
        {
            return false;
        }

        // Quando a distancia ja e pequena e o agent praticamente parou, consideramos chegada.
        return !agenteNpcAtual.hasPath || agenteNpcAtual.velocity.sqrMagnitude <= 0.01f;
    }

    bool MoverNpcAte(Vector3 destino, float distanciaChegada)
    {
        // Fallback de movimento manual, usado quando o NavMeshAgent nao esta disponivel.
        if (npcAtual == null)
        {
            return false;
        }

        Vector3 posicaoAtual = npcAtual.transform.position;
        Vector3 destinoPlano = new Vector3(destino.x, posicaoAtual.y, destino.z);
        Vector3 direcao = destinoPlano - posicaoAtual;
        direcao.y = 0f;
        float distanciaAteDestino = direcao.magnitude;

        if (distanciaAteDestino <= distanciaChegada)
        {
            // Encaixa o cliente no destino ao chegar perto o bastante.
            if (!MovimentoNpcEstaLivre(posicaoAtual, destinoPlano - posicaoAtual))
            {
                velocidadeNpcAtual = 0f;
                return false;
            }

            velocidadeNpcAtual = 0f;
            npcAtual.transform.position = destinoPlano;
            return true;
        }

        Vector3 direcaoNormalizada = direcao / Mathf.Max(distanciaAteDestino, 0.0001f);
        float velocidadeMaximaCurva = CalcularVelocidadeMaximaEmCurva(direcaoNormalizada);
        float velocidadeAlvo = CalcularVelocidadeAlvo(distanciaAteDestino, velocidadeMaximaCurva);
        float aceleracaoAtual = velocidadeNpcAtual < velocidadeAlvo ? aceleracaoNpc : desaceleracaoNpc;
        velocidadeNpcAtual = Mathf.MoveTowards(velocidadeNpcAtual, velocidadeAlvo, aceleracaoAtual * Time.deltaTime);

        Vector3 movimentoDesejado = direcaoNormalizada * Mathf.Min(velocidadeNpcAtual * Time.deltaTime, distanciaAteDestino);
        Vector3 novaPosicao = posicaoAtual;
        Vector3 direcaoMovimentoReal = direcaoNormalizada;

        if (MovimentoNpcEstaLivre(posicaoAtual, movimentoDesejado))
        {
            // Move normalmente quando o passo atual estiver livre.
            novaPosicao = posicaoAtual + movimentoDesejado;
        }
        else if (!TentarDesviarNpc(posicaoAtual, movimentoDesejado, direcao, out novaPosicao))
        {
            // Se houver parede ou MeshCollider no caminho, o NPC para e espera um caminho livre.
            velocidadeNpcAtual = Mathf.MoveTowards(velocidadeNpcAtual, 0f, desaceleracaoNpc * Time.deltaTime);
            return false;
        }
        else
        {
            Vector3 movimentoExecutado = novaPosicao - posicaoAtual;
            if (movimentoExecutado.sqrMagnitude > 0.0001f)
            {
                direcaoMovimentoReal = movimentoExecutado.normalized;
            }
        }

        npcAtual.transform.position = novaPosicao;
        SuavizarRotacaoNpc(direcaoMovimentoReal);

        if (direcaoMovimentoReal.sqrMagnitude > 0.0001f)
        {
            ultimaDirecaoMovimentoNpc = direcaoMovimentoReal;
        }

        return false;
    }

    float CalcularVelocidadeAlvo(float distanciaAteDestino, float velocidadeMaximaCurva)
    {
        float distanciaFreioNormalizada = Mathf.Max(distanciaChegadaWaypoint + 0.05f, distanciaFreioNpc);
        float fatorDistancia = Mathf.InverseLerp(distanciaChegadaWaypoint, distanciaFreioNormalizada, distanciaAteDestino);
        float velocidadeMinima = Mathf.Min(velocidadeMaximaCurva, velocidadeNpc * 0.2f);
        return Mathf.Lerp(velocidadeMinima, velocidadeMaximaCurva, fatorDistancia);
    }

    float CalcularVelocidadeMaximaEmCurva(Vector3 direcaoAtual)
    {
        if (ultimaDirecaoMovimentoNpc.sqrMagnitude <= 0.0001f || direcaoAtual.sqrMagnitude <= 0.0001f)
        {
            return velocidadeNpc;
        }

        float anguloCurva = Vector3.Angle(ultimaDirecaoMovimentoNpc, direcaoAtual);
        float fatorCurva = Mathf.InverseLerp(180f, 0f, anguloCurva);
        return Mathf.Lerp(velocidadeNpc * velocidadeMinimaEmCurva, velocidadeNpc, fatorCurva);
    }

    void SuavizarRotacaoNpc(Vector3 direcaoDesejada)
    {
        if (npcAtual == null)
        {
            return;
        }

        Vector3 direcaoPlana = direcaoDesejada;
        direcaoPlana.y = 0f;
        if (direcaoPlana.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // Gira o corpo de forma suave para evitar aquelas viradas secas nos waypoints.
        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoPlana.normalized, Vector3.up);
        npcAtual.transform.rotation = Quaternion.Slerp(
            npcAtual.transform.rotation,
            rotacaoAlvo,
            velocidadeRotacaoNpc * Time.deltaTime
        );
    }

    bool MovimentoNpcEstaLivre(Vector3 posicaoAtual, Vector3 movimento)
    {
        float distancia = movimento.magnitude;
        if (distancia <= 0.0001f)
        {
            return true;
        }

        Vector3 direcao = movimento.normalized;
        float raioCapsula = Mathf.Max(0.05f, raioColisaoNpc);
        float alturaCapsula = Mathf.Max(raioCapsula * 2f + 0.05f, alturaColisaoNpc);
        Vector3 baseCapsula = posicaoAtual + Vector3.up * raioCapsula;
        Vector3 topoCapsula = posicaoAtual + Vector3.up * (alturaCapsula - raioCapsula);
        RaycastHit[] hits = Physics.CapsuleCastAll(
            baseCapsula,
            topoCapsula,
            raioCapsula,
            direcao,
            distancia + margemColisaoNpc,
            mascaraColisaoNpc,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider colliderAtingido = hits[i].collider;
            if (colliderAtingido == null)
            {
                continue;
            }

            if (npcAtual != null && colliderAtingido.transform.IsChildOf(npcAtual.transform))
            {
                continue;
            }

            // Qualquer collider fisico, incluindo MeshCollider, bloqueia o passo do NPC.
            return false;
        }

        return true;
    }

    bool ExisteCaminhoLivreEntrePontos(Vector3 origem, Vector3 destino)
    {
        if (navMeshPronto && ExisteCaminhoNavMeshEntrePontos(origem, destino))
        {
            // Se a NavMesh disser que existe rota valida, deixamos o agent resolver o desvio real.
            return true;
        }

        Vector3 movimento = destino - origem;
        movimento.y = 0f;
        return MovimentoNpcEstaLivre(origem, movimento);
    }

    bool ExisteCaminhoNavMeshEntrePontos(Vector3 origem, Vector3 destino)
    {
        if (!usarNavMesh || !navMeshPronto)
        {
            return false;
        }

        if (!TentarProjetarNoNavMesh(origem, out Vector3 origemNavMesh) || !TentarProjetarNoNavMesh(destino, out Vector3 destinoNavMesh))
        {
            return false;
        }

        NavMeshPath caminhoCalculado = new NavMeshPath();
        if (!NavMesh.CalculatePath(origemNavMesh, destinoNavMesh, NavMesh.AllAreas, caminhoCalculado))
        {
            return false;
        }

        return caminhoCalculado.status == NavMeshPathStatus.PathComplete;
    }

    bool TentarDesviarNpc(Vector3 posicaoAtual, Vector3 movimentoDesejado, Vector3 direcaoAlvo, out Vector3 novaPosicao)
    {
        novaPosicao = posicaoAtual;

        float distancia = movimentoDesejado.magnitude;
        if (distancia <= 0.0001f)
        {
            return false;
        }

        Vector3 direcaoBase = movimentoDesejado.normalized;
        Vector3 direcaoAlvoNormalizada = direcaoAlvo.sqrMagnitude > 0.0001f ? direcaoAlvo.normalized : direcaoBase;
        float[] angulosTentativa = { 30f, -30f, 55f, -55f };

        for (int i = 0; i < angulosTentativa.Length; i++)
        {
            Vector3 direcaoDesvio = Quaternion.AngleAxis(angulosTentativa[i], Vector3.up) * direcaoBase;
            if (Vector3.Dot(direcaoDesvio, direcaoAlvoNormalizada) <= -0.1f)
            {
                continue;
            }

            Vector3 movimentoDesvio = direcaoDesvio * distancia;
            if (MovimentoNpcEstaLivre(posicaoAtual, movimentoDesvio))
            {
                // Quando o caminho direto estiver bloqueado, tenta um pequeno desvio lateral.
                novaPosicao = posicaoAtual + movimentoDesvio;
                return true;
            }
        }

        return false;
    }

    void AoChegarNoCaixa()
    {
        // Quando o NPC chega ao caixa, coloca o produto no checkout e pede o scan.
        // Quando o cliente chega ao caixa, ele coloca o item que carregou no checkout.
        produtoAtual = ColocarProdutoCarregadoNoCaixa();

        if (produtoAtual == null)
        {
            scanner.MostrarTextoInfoProduto("Cliente chegou ao caixa, mas nenhum produto foi encontrado.", 2f);
            indiceWaypointSaidaAtual = 0;
            estadoAtual = EstadoCliente.IndoEmbora;
            return;
        }

        // Mostra os dados do item e pede o escaneamento.
        scanner.MostrarTextoInfoProduto(
            "Cliente no caixa.\n\n" +
            produtoAtual.ObterMensagemEscaneamento() +
            "\n\nEscaneie o produto para continuar.",
            0f
        );
    }

    void AoChegarNoProduto()
    {
        // Quando o NPC chega perto do produto, cria o produto visual carregado e segue ao caixa.
        CriarProdutoCarregadoPeloNpc();

        if (produtoDestinoAtual != null && !EstaDentroDeInteractables(produtoDestinoAtual.ObterRaizProduto()))
        {
            // Quando o NPC chega ao produto visual da loja, ele some da prateleira.
            produtoDestinoAtual.EsconderProdutoVisualDaLoja();
        }

        // Ao terminar a busca pelo produto, o cliente segue a segunda metade da rota ate o caixa.
        indiceWaypointEntradaAtual = 0;
    }

    void AoProdutoEscaneado(Pickup pickup)
    {
        if (estadoAtual != EstadoCliente.AguardandoEscaneamento)
        {
            return;
        }

        if (pickup != produtoAtual)
        {
            return;
        }

        // Assim que o item correto passa no scanner, calcula total, pagamento e troco.
        totalCompra = pickup.ObterPrecoProduto();
        valorPago = SortearValorPago(totalCompra);
        troco = Mathf.Max(0f, valorPago - totalCompra);

        estadoAtual = EstadoCliente.AguardandoTroco;
        produtoAtual = null;
        trocoDigitadoEmCentavos = string.Empty;
        avisoTrocoAtual = string.Empty;
        mensagemTrocoAtual =
            pickup.ObterMensagemEscaneamento() +
            "\n\nTotal: R$ " + FormatarDinheiro(totalCompra) +
            "\nPago: R$ " + FormatarDinheiro(valorPago) +
            "\nDigite o troco e aperte Enter.";

        AtualizarTextoTroco();
    }

    void EntregarTroco()
    {
        // Finaliza o atendimento quando o jogador digita o troco correto.
        if (goalManager != null)
        {
            // Conta o cliente como atendido e informa o valor entregue.
            goalManager.ClienteAtendido();
            goalManager.MostrarMensagem("Troco entregue: R$ " + FormatarDinheiro(troco), 3f);
        }

        scanner.MostrarTextoInfoProduto("Troco entregue.\nCliente indo embora.", 2f);
        trocoDigitadoEmCentavos = string.Empty;
        mensagemTrocoAtual = string.Empty;
        avisoTrocoAtual = string.Empty;
        indiceWaypointSaidaAtual = 0;
        estadoAtual = EstadoCliente.IndoEmbora;
    }

    void AoClienteIrEmbora()
    {
        if (npcAtual != null)
        {
            // Remove o clone do cliente quando ele chega ao fim da rota de saida.
            Destroy(npcAtual);
            npcAtual = null;
        }

        estadoAtual = EstadoCliente.AguardandoProximoCliente;
    }

    void ProcessarDigitacaoTroco()
    {
        bool houveMudanca = false;

        // Le cada caractere digitado pelo jogador neste frame.
        foreach (char caractere in Input.inputString)
        {
            if (char.IsDigit(caractere))
            {
                if (trocoDigitadoEmCentavos.Length < maximoDigitosTroco)
                {
                    trocoDigitadoEmCentavos += caractere;
                    avisoTrocoAtual = string.Empty;
                    houveMudanca = true;
                }
            }
            else if (caractere == '\b')
            {
                if (trocoDigitadoEmCentavos.Length > 0)
                {
                    trocoDigitadoEmCentavos = trocoDigitadoEmCentavos.Substring(0, trocoDigitadoEmCentavos.Length - 1);
                    avisoTrocoAtual = string.Empty;
                    houveMudanca = true;
                }
            }
            else if (caractere == '\r' || caractere == '\n')
            {
                ConfirmarTrocoDigitado();
                return;
            }
        }

        if (houveMudanca)
        {
            AtualizarTextoTroco();
        }
    }

    void ConfirmarTrocoDigitado()
    {
        if (!JogadorPertoDoScanner())
        {
            // Impede confirmar troco de longe para manter a logica do caixa coerente.
            avisoTrocoAtual = "Chegue mais perto do caixa para confirmar o troco.";
            AtualizarTextoTroco();
            return;
        }

        float valorDigitado = ObterValorTrocoDigitado();
        if (Mathf.Abs(valorDigitado - troco) <= 0.009f)
        {
            EntregarTroco();
            return;
        }

        // Limpa a digitacao quando o troco estiver errado e pede uma nova tentativa.
        trocoDigitadoEmCentavos = string.Empty;
        avisoTrocoAtual = "Troco incorreto. Digite novamente.";
        AtualizarTextoTroco();
    }

    void AtualizarTextoTroco()
    {
        if (scanner == null)
        {
            return;
        }

        // Monta a mensagem completa mostrada no Canvas durante a digitacao do troco.
        string textoTroco =
            mensagemTrocoAtual +
            "\nTroco digitado: R$ " + FormatarTrocoDigitado();

        if (!string.IsNullOrWhiteSpace(avisoTrocoAtual))
        {
            textoTroco += "\n" + avisoTrocoAtual;
        }

        scanner.MostrarTextoInfoProduto(textoTroco, 0f);
    }

    void PrepararPontosDaCena()
    {
        // Procura automaticamente objetos importantes da cena pelo nome.
        // Isso ajuda o sistema a funcionar mesmo sem tudo ligado manualmente no Inspector.
        if (spawnNPC == null)
        {
            GameObject spawnNpcObject = GameObject.Find("SpawnNPC");
            if (spawnNpcObject != null)
            {
                spawnNPC = spawnNpcObject.transform;
            }
        }

        if (despawnNPC == null)
        {
            GameObject despawnNpcObject = GameObject.Find("DespawnNPC");
            if (despawnNpcObject != null)
            {
                despawnNPC = despawnNpcObject.transform;
            }
        }

        if (spawnProduto == null)
        {
            GameObject spawnProdutoObject = GameObject.Find("SpawnObjects");
            if (spawnProdutoObject != null)
            {
                spawnProduto = spawnProdutoObject.transform;
            }
        }

        if (pontoParadaCaixa == null)
        {
            GameObject pontoParadaObject = GameObject.Find("ScannerNPC");
            if (pontoParadaObject != null)
            {
                pontoParadaCaixa = pontoParadaObject.transform;
            }
        }

        if (raizProdutosInteragiveis == null)
        {
            GameObject interactablesObject = GameObject.Find("Interactables");
            if (interactablesObject != null)
            {
                raizProdutosInteragiveis = interactablesObject.transform;
            }
        }

        if (raizProdutosVisuaisLoja == null)
        {
            GameObject visuaisLojaObject = GameObject.Find("ProdutosVisuaisLoja");
            if (visuaisLojaObject != null)
            {
                raizProdutosVisuaisLoja = visuaisLojaObject.transform;
            }
        }

        if (raizProdutosAtendimentoNpc == null && raizProdutosVisuaisLoja != null)
        {
            // Por padrao, o mesmo grupo de produtos visuais tambem vira a area de atendimento do NPC.
            raizProdutosAtendimentoNpc = raizProdutosVisuaisLoja;
        }

        if (raizAreaNavMesh == null)
        {
            GameObject lojaObject = GameObject.Find("shop");
            if (lojaObject != null)
            {
                // Usa a raiz da loja como limite padrao da navegacao.
                raizAreaNavMesh = lojaObject.transform;
            }
        }
    }

    void PrepararNavMeshDaLoja()
    {
        // Prepara a superficie de navegacao usada pelo NPC.
        navMeshPronto = false;

        if (!usarNavMesh)
        {
            return;
        }

        if (superficieNavMesh == null)
        {
            superficieNavMesh = GetComponent<NavMeshSurface>();
        }

        if (superficieNavMesh == null)
        {
            // Tambem aceita uma NavMeshSurface colocada em qualquer outro objeto da cena.
            superficieNavMesh = Object.FindFirstObjectByType<NavMeshSurface>();
        }

        if (superficieNavMesh == null && construirNavMeshEmRuntime)
        {
            // Cria a superficie no proprio controlador para nao exigir configuracao manual extra.
            superficieNavMesh = gameObject.AddComponent<NavMeshSurface>();
        }

        if (superficieNavMesh == null)
        {
            Debug.LogWarning("SistemaCaixa nao encontrou NavMeshSurface. Adicione uma na cena e use Bake para o NPC andar pelo mercado.", this);
            return;
        }

        ConfigurarSuperficieNavMesh();

        if (construirNavMeshEmRuntime)
        {
            // Runtime bake e opcional porque meshes importadas sem Read/Write costumam gerar erro no Play.
            superficieNavMesh.BuildNavMesh();
        }
        else if (superficieNavMesh.navMeshData == null)
        {
            Debug.LogWarning("SistemaCaixa encontrou a NavMeshSurface, mas ela ainda nao foi assada. Selecione a superficie e clique em Bake antes de dar Play.", superficieNavMesh);
        }

        Vector3 pontoReferencia = pontoParadaCaixa != null ? pontoParadaCaixa.position : CalcularPontoParadaNoCaixa();
        navMeshPronto = TentarProjetarNoNavMesh(pontoReferencia, out _);

        if (!navMeshPronto)
        {
            Debug.LogWarning("SistemaCaixa nao encontrou uma NavMesh valida perto do caixa. Confira se o chao do mercado entrou no Bake.", this);
        }
    }

    void ConfigurarSuperficieNavMesh()
    {
        if (superficieNavMesh == null)
        {
            return;
        }

        // A superficie usa as colisoes reais da loja para montar a area navegavel do NPC.
        superficieNavMesh.agentTypeID = ObterAgentTypeNavMeshPadrao();
        superficieNavMesh.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        superficieNavMesh.layerMask = mascaraColisaoNpc;
        superficieNavMesh.ignoreNavMeshAgent = true;
        superficieNavMesh.ignoreNavMeshObstacle = false;
        superficieNavMesh.overrideVoxelSize = true;
        superficieNavMesh.voxelSize = Mathf.Max(0.05f, raioColisaoNpc * 0.5f);
        superficieNavMesh.overrideTileSize = true;
        superficieNavMesh.tileSize = 128;

        if (limitarNavMeshAoMercado && TentarCalcularBoundsAreaNavMesh(out Bounds boundsMercado))
        {
            // Usa apenas um volume do mercado para impedir que a malha pegue a area de fora.
            superficieNavMesh.collectObjects = CollectObjects.Volume;
            superficieNavMesh.center = superficieNavMesh.transform.InverseTransformPoint(boundsMercado.center);
            superficieNavMesh.size = SomarMargemAoBounds(boundsMercado.size);
            return;
        }

        // Fallback para pegar a cena toda quando nao existir uma raiz valida da loja.
        superficieNavMesh.collectObjects = CollectObjects.All;
    }

    bool TentarCalcularBoundsAreaNavMesh(out Bounds boundsMercado)
    {
        boundsMercado = new Bounds();

        if (raizAreaNavMesh == null)
        {
            return false;
        }

        Collider[] collidersLoja = raizAreaNavMesh.GetComponentsInChildren<Collider>(true);
        bool possuiBounds = false;

        for (int i = 0; i < collidersLoja.Length; i++)
        {
            Collider colliderAtual = collidersLoja[i];
            if (colliderAtual == null)
            {
                continue;
            }

            if (!possuiBounds)
            {
                boundsMercado = colliderAtual.bounds;
                possuiBounds = true;
            }
            else
            {
                boundsMercado.Encapsulate(colliderAtual.bounds);
            }
        }

        if (possuiBounds)
        {
            EncapsularPontosImportantesDaNavegacao(ref boundsMercado);
            return true;
        }

        Renderer[] renderersLoja = raizAreaNavMesh.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderersLoja.Length; i++)
        {
            Renderer rendererAtual = renderersLoja[i];
            if (rendererAtual == null)
            {
                continue;
            }

            if (!possuiBounds)
            {
                boundsMercado = rendererAtual.bounds;
                possuiBounds = true;
            }
            else
            {
                boundsMercado.Encapsulate(rendererAtual.bounds);
            }
        }

        if (possuiBounds)
        {
            EncapsularPontosImportantesDaNavegacao(ref boundsMercado);
        }

        return possuiBounds;
    }

    void EncapsularPontosImportantesDaNavegacao(ref Bounds boundsMercado)
    {
        EncapsularPontoNavMesh(ref boundsMercado, spawnNPC);
        EncapsularPontoNavMesh(ref boundsMercado, despawnNPC);
        EncapsularPontoNavMesh(ref boundsMercado, pontoParadaCaixa);
        EncapsularPontoNavMesh(ref boundsMercado, spawnProduto);

        if (pontosSpawn != null)
        {
            for (int i = 0; i < pontosSpawn.Length; i++)
            {
                EncapsularPontoNavMesh(ref boundsMercado, pontosSpawn[i]);
            }
        }
    }

    void EncapsularPontoNavMesh(ref Bounds boundsMercado, Transform ponto)
    {
        if (ponto == null)
        {
            return;
        }

        boundsMercado.Encapsulate(ponto.position);
    }

    Vector3 SomarMargemAoBounds(Vector3 tamanhoOriginal)
    {
        // Expande o volume so um pouco para nao cortar a entrada e os corredores por detalhe.
        return new Vector3(
            Mathf.Max(0.5f, tamanhoOriginal.x + margemAreaNavMesh.x),
            Mathf.Max(0.5f, tamanhoOriginal.y + margemAreaNavMesh.y),
            Mathf.Max(0.5f, tamanhoOriginal.z + margemAreaNavMesh.z)
        );
    }

    int ObterAgentTypeNavMeshPadrao()
    {
        int quantidadeConfiguracoes = NavMesh.GetSettingsCount();
        if (quantidadeConfiguracoes > 0)
        {
            // Usa o primeiro agent type cadastrado no projeto para manter compatibilidade com a cena.
            return NavMesh.GetSettingsByIndex(0).agentTypeID;
        }

        return 0;
    }

    void ConfigurarAgenteNpc(GameObject npcClonado)
    {
        agenteNpcAtual = null;
        destinoNavMeshDefinido = false;
        ultimoDestinoNavMesh = Vector3.zero;

        if (!usarNavMesh || !navMeshPronto || npcClonado == null)
        {
            return;
        }

        agenteNpcAtual = npcClonado.GetComponent<NavMeshAgent>();
        if (agenteNpcAtual == null)
        {
            // O agent fica so no clone de runtime para nao poluir o objeto-base da cena.
            agenteNpcAtual = npcClonado.AddComponent<NavMeshAgent>();
        }

        agenteNpcAtual.enabled = false;
        agenteNpcAtual.radius = Mathf.Max(0.05f, raioColisaoNpc);
        agenteNpcAtual.height = Mathf.Max((raioColisaoNpc * 2f) + 0.05f, alturaColisaoNpc);
        agenteNpcAtual.speed = velocidadeNpc;
        agenteNpcAtual.acceleration = aceleracaoNavMesh;
        agenteNpcAtual.angularSpeed = velocidadeAngularNavMesh;
        agenteNpcAtual.stoppingDistance = Mathf.Max(distanciaChegadaNavMesh, distanciaChegadaWaypoint);
        agenteNpcAtual.autoBraking = true;
        agenteNpcAtual.autoRepath = true;
        agenteNpcAtual.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agenteNpcAtual.avoidancePriority = 50;
        agenteNpcAtual.updatePosition = true;
        agenteNpcAtual.updateRotation = true;

        if (!TentarProjetarSpawnDoNpcNoNavMesh(npcClonado.transform.position, out Vector3 posicaoNavMesh))
        {
            Debug.LogWarning("SistemaCaixa nao conseguiu encaixar o NPC no NavMesh. O clone vai usar o fallback antigo.", npcClonado);
            agenteNpcAtual = null;
            return;
        }

        agenteNpcAtual.enabled = true;
        agenteNpcAtual.Warp(posicaoNavMesh);
        npcClonado.transform.position = posicaoNavMesh;
    }

    bool TentarProjetarNoNavMesh(Vector3 posicaoDesejada, out Vector3 posicaoNavMesh)
    {
        posicaoNavMesh = posicaoDesejada;

        if (!usarNavMesh)
        {
            return false;
        }

        if (NavMesh.SamplePosition(posicaoDesejada, out NavMeshHit hitNavMesh, raioAmostraNavMesh, NavMesh.AllAreas))
        {
            // Usa o ponto valido mais proximo da malha para o NPC nao nascer congelado fora dela.
            posicaoNavMesh = hitNavMesh.position;
            return true;
        }

        return false;
    }

    bool TentarProjetarSpawnDoNpcNoNavMesh(Vector3 posicaoDesejada, out Vector3 posicaoNavMesh)
    {
        if (TentarProjetarNoNavMesh(posicaoDesejada, out posicaoNavMesh))
        {
            return true;
        }

        float raioBuscaEntrada = Mathf.Max(raioAmostraNavMesh, 8f);
        if (NavMesh.SamplePosition(posicaoDesejada, out NavMeshHit hitNavMesh, raioBuscaEntrada, NavMesh.AllAreas))
        {
            posicaoNavMesh = hitNavMesh.position;
            return true;
        }

        posicaoNavMesh = posicaoDesejada;
        return false;
    }

    void PrepararConfiguracoesClientes()
    {
        if (configuracoesClientes == null || configuracoesClientes.Length == 0)
        {
            configuracoesClientes = new ConfiguracaoClienteCaixa[8];
        }

        for (int i = 0; i < configuracoesClientes.Length; i++)
        {
            if (configuracoesClientes[i] == null)
            {
                // Cria slots vazios para o Inspector e o runtime manterem o mesmo formato.
                configuracoesClientes[i] = new ConfiguracaoClienteCaixa();
            }
        }
    }

    void PrepararIndicesConfiguracoesValidas()
    {
        indicesConfiguracoesValidas.Clear();

        for (int i = 0; i < configuracoesClientes.Length; i++)
        {
            ConfiguracaoClienteCaixa configuracao = configuracoesClientes[i];
            if (configuracao == null)
            {
                continue;
            }

            if (configuracao.npc == null || configuracao.produto == null)
            {
                continue;
            }

            // So entra no sorteio quem tem pelo menos NPC e produto.
            indicesConfiguracoesValidas.Add(i);
        }
    }

    bool TentarEscolherConfiguracaoCliente()
    {
        if (indicesConfiguracoesValidas.Count == 0)
        {
            return false;
        }

        int indiceConfiguracao = indicesConfiguracoesValidas[Random.Range(0, indicesConfiguracoesValidas.Count)];

        if (evitarRepetirNpcEmSequencia && indicesConfiguracoesValidas.Count > 1)
        {
            int tentativas = 0;
            while (indiceConfiguracao == ultimoIndiceConfiguracao && tentativas < 10)
            {
                indiceConfiguracao = indicesConfiguracoesValidas[Random.Range(0, indicesConfiguracoesValidas.Count)];
                tentativas++;
            }
        }

        ultimoIndiceConfiguracao = indiceConfiguracao;
        configuracaoAtual = configuracoesClientes[indiceConfiguracao];
        return configuracaoAtual != null;
    }

    void PrepararRotaEntradaAtual(ConfiguracaoClienteCaixa configuracao)
    {
        caminhoEntradaAtual.Clear();

        if (configuracao != null && TentarCopiarPontosOrdenados(configuracao.caminhoEntrada, caminhoEntradaAtual))
        {
            return;
        }

        if (TentarUsarRotaAleatoria(rotasEntradaAleatorias, caminhoEntradaAtual, ref ultimoIndiceRotaEntrada))
        {
            return;
        }

        PrepararCaminhoAtual(caminhoEntrada, caminhoEntradaAtual, sortearCaminhoEntrada);
    }

    void PrepararRotaSaidaAtual(ConfiguracaoClienteCaixa configuracao)
    {
        caminhoSaidaAtual.Clear();

        if (configuracao != null && TentarCopiarPontosOrdenados(configuracao.caminhoSaida, caminhoSaidaAtual))
        {
            return;
        }

        if (TentarUsarRotaAleatoria(rotasSaidaAleatorias, caminhoSaidaAtual, ref ultimoIndiceRotaSaida))
        {
            return;
        }

        PrepararCaminhoAtual(caminhoSaida, caminhoSaidaAtual, sortearCaminhoSaida);
    }

    bool TentarPrepararCaminhosAutomaticosParaProduto()
    {
        caminhoProdutoAtual.Clear();
        caminhoEntradaAtual.Clear();
        produtoDestinoAtual = ObterProdutoDaLojaCorrespondente(modeloProdutoAtual);
        possuiPosicaoParadaProdutoAtual = false;

        if (produtoDestinoAtual == null || pontoParadaCaixa == null)
        {
            return false;
        }

        Transform pontoSpawn = ObterPontoSpawnCliente();
        if (pontoSpawn == null)
        {
            return false;
        }

        posicaoParadaProdutoAtual = CalcularPontoParadaNoProduto(produtoDestinoAtual, pontoSpawn.position);
        possuiPosicaoParadaProdutoAtual = true;

        // Monta um trecho automatico do spawn ate um ponto caminhavel ao lado do produto.
        bool trechoAteProdutoValido = TentarConstruirCaminhoAutomatico(
            pontoSpawn.position,
            posicaoParadaProdutoAtual,
            caminhoProdutoAtual,
            minimoPontosAteProduto,
            maximoPontosAteProduto
        );

        if (caminhoProdutoAtual.Count > 0)
        {
            posicaoParadaProdutoAtual = CalcularPontoParadaNoProduto(produtoDestinoAtual, caminhoProdutoAtual[caminhoProdutoAtual.Count - 1].position);
        }

        InserirPasseioAleatorioAntesDoProduto(pontoSpawn.position, posicaoParadaProdutoAtual, caminhoProdutoAtual);

        // Monta o segundo trecho do produto ate o caixa usando o mesmo pool de pontos.
        bool trechoAteCaixaValido = TentarConstruirCaminhoAutomatico(
            posicaoParadaProdutoAtual,
            CalcularPontoParadaNoCaixa(),
            caminhoEntradaAtual,
            minimoPontosAteCaixa,
            maximoPontosAteCaixa
        );

        if (!trechoAteProdutoValido || !trechoAteCaixaValido)
        {
            // Se a rota automatica nao ficar realmente livre, cai no sistema antigo em vez de travar o NPC.
            caminhoProdutoAtual.Clear();
            caminhoEntradaAtual.Clear();
            produtoDestinoAtual = null;
            possuiPosicaoParadaProdutoAtual = false;
            posicaoParadaProdutoAtual = Vector3.zero;
            return false;
        }

        return true;
    }

    void InserirPasseioAleatorioAntesDoProduto(Vector3 origem, Vector3 destinoProduto, List<Transform> caminhoDestino)
    {
        if (maximoPontosPasseioAntesProduto <= 0 || pontosCaminhoAutomatico.Count == 0)
        {
            return;
        }

        int quantidadePasseio = Random.Range(0, maximoPontosPasseioAntesProduto + 1);
        if (quantidadePasseio <= 0)
        {
            return;
        }

        List<Transform> candidatos = new List<Transform>(pontosCaminhoAutomatico);
        EmbaralharCaminho(candidatos);

        List<Transform> passeio = new List<Transform>();
        Vector3 posicaoAtual = origem;

        for (int i = 0; i < candidatos.Count && passeio.Count < quantidadePasseio; i++)
        {
            Transform candidato = candidatos[i];
            if (candidato == null || caminhoDestino.Contains(candidato))
            {
                continue;
            }

            if (!ExisteCaminhoLivreEntrePontos(posicaoAtual, candidato.position))
            {
                continue;
            }

            if (!ExisteCaminhoLivreEntrePontos(candidato.position, destinoProduto))
            {
                continue;
            }

            passeio.Add(candidato);
            posicaoAtual = candidato.position;
        }

        for (int i = passeio.Count - 1; i >= 0; i--)
        {
            caminhoDestino.Insert(0, passeio[i]);
        }
    }

    bool TentarUsarRotaAleatoria(RotaWaypoint[] rotas, List<Transform> destino, ref int ultimoIndiceRota)
    {
        if (rotas == null || rotas.Length == 0)
        {
            return false;
        }

        List<int> indicesValidos = new List<int>();
        for (int i = 0; i < rotas.Length; i++)
        {
            if (rotas[i] == null)
            {
                continue;
            }

            if (rotas[i].pontos == null || rotas[i].pontos.Length == 0)
            {
                continue;
            }

            indicesValidos.Add(i);
        }

        if (indicesValidos.Count == 0)
        {
            return false;
        }

        int indiceRota = indicesValidos[Random.Range(0, indicesValidos.Count)];

        if (indicesValidos.Count > 1)
        {
            int tentativas = 0;
            while (indiceRota == ultimoIndiceRota && tentativas < 10)
            {
                indiceRota = indicesValidos[Random.Range(0, indicesValidos.Count)];
                tentativas++;
            }
        }

        ultimoIndiceRota = indiceRota;
        return TentarCopiarPontosOrdenados(rotas[indiceRota].pontos, destino);
    }

    bool TentarCopiarPontosOrdenados(Transform[] pontosOrigem, List<Transform> destino)
    {
        destino.Clear();

        if (pontosOrigem == null || pontosOrigem.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < pontosOrigem.Length; i++)
        {
            if (pontosOrigem[i] != null)
            {
                // Copia a rota exatamente na ordem em que ela foi definida no Inspector.
                destino.Add(pontosOrigem[i]);
            }
        }

        return destino.Count > 0;
    }

    void PrepararCaminhoAtual(Transform[] caminhoBase, List<Transform> caminhoAtual, bool sortearPontos)
    {
        caminhoAtual.Clear();

        if (caminhoBase == null || caminhoBase.Length == 0)
        {
            return;
        }

        for (int i = 0; i < caminhoBase.Length; i++)
        {
            if (caminhoBase[i] != null)
            {
                // Copia os pontos validos do fallback antigo para a rota atual.
                caminhoAtual.Add(caminhoBase[i]);
            }
        }

        if (sortearPontos)
        {
            // Fallback antigo que embaralha a lista inteira quando solicitado.
            EmbaralharCaminho(caminhoAtual);
        }
    }

    bool TentarConstruirCaminhoAutomatico(Vector3 origem, Vector3 destino, List<Transform> caminhoDestino, int minimoPontos, int maximoPontos)
    {
        caminhoDestino.Clear();

        if (ExisteCaminhoLivreEntrePontos(origem, destino))
        {
            return true;
        }

        if (pontosCaminhoAutomatico.Count == 0)
        {
            return false;
        }

        int minimoNormalizado = Mathf.Max(0, minimoPontos);
        int maximoNormalizado = Mathf.Max(minimoNormalizado, maximoPontos);
        int quantidadeDesejada = Random.Range(minimoNormalizado, maximoNormalizado + 1);
        Vector3 posicaoAtual = origem;
        List<Transform> candidatosDisponiveis = new List<Transform>(pontosCaminhoAutomatico);

        while (caminhoDestino.Count < quantidadeDesejada && candidatosDisponiveis.Count > 0)
        {
            Transform melhorPonto = null;
            float melhorScore = float.MaxValue;
            float distanciaAtualAoDestino = DistanciaPlana(posicaoAtual, destino);

            for (int i = candidatosDisponiveis.Count - 1; i >= 0; i--)
            {
                Transform candidato = candidatosDisponiveis[i];
                if (candidato == null)
                {
                    candidatosDisponiveis.RemoveAt(i);
                    continue;
                }

                float distanciaDoPontoAoDestino = DistanciaPlana(candidato.position, destino);
                if (distanciaDoPontoAoDestino >= distanciaAtualAoDestino - toleranciaMelhoraWaypoint)
                {
                    continue;
                }

                if (!ExisteCaminhoLivreEntrePontos(posicaoAtual, candidato.position))
                {
                    continue;
                }

                float score = DistanciaPlana(posicaoAtual, candidato.position) + (distanciaDoPontoAoDestino * 0.75f);
                if (score < melhorScore)
                {
                    melhorScore = score;
                    melhorPonto = candidato;
                }
            }

            if (melhorPonto == null)
            {
                break;
            }

            // Escolhe o proximo ponto que mais aproxima o cliente do destino final.
            caminhoDestino.Add(melhorPonto);
            candidatosDisponiveis.Remove(melhorPonto);
            posicaoAtual = melhorPonto.position;

            if (caminhoDestino.Count >= minimoNormalizado && ExisteCaminhoLivreEntrePontos(posicaoAtual, destino))
            {
                return true;
            }
        }

        return ExisteCaminhoLivreEntrePontos(posicaoAtual, destino) && (caminhoDestino.Count > 0 || minimoNormalizado == 0);
    }

    void EmbaralharCaminho(List<Transform> caminho)
    {
        for (int i = caminho.Count - 1; i > 0; i--)
        {
            // Fisher-Yates cria uma ordem aleatoria sem repetir pontos.
            int indiceSorteado = Random.Range(0, i + 1);
            Transform pontoTemporario = caminho[i];
            caminho[i] = caminho[indiceSorteado];
            caminho[indiceSorteado] = pontoTemporario;
        }
    }

    void RemoverPontoDoCaminho(List<Transform> caminho, Transform pontoIgnorado)
    {
        if (pontoIgnorado == null)
        {
            return;
        }

        for (int i = caminho.Count - 1; i >= 0; i--)
        {
            if (caminho[i] == pontoIgnorado)
            {
                // Evita usar pontos tecnicos como waypoint intermediario por engano.
                caminho.RemoveAt(i);
            }
        }
    }

    void PrepararModelosNpc()
    {
        // Monta a lista de NPCs possiveis usando campos do Inspector e objetos NPC da cena.
        modelosNpc.Clear();

        AdicionarModeloNpc(npcCliente);

        if (npcsExtras != null)
        {
            for (int i = 0; i < npcsExtras.Length; i++)
            {
                AdicionarModeloNpc(npcsExtras[i]);
            }
        }

        if (modelosNpc.Count == 0)
        {
            GameObject[] objetosDaCena = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objetosDaCena.Length; i++)
            {
                GameObject objeto = objetosDaCena[i];
                if (!objeto.scene.IsValid())
                {
                    continue;
                }

                if (!objeto.name.StartsWith("NPC"))
                {
                    continue;
                }

                AdicionarModeloNpc(objeto);
            }
        }

        // Ordena para parear NPC1 com o primeiro produto, NPC2 com o segundo e assim por diante.
        modelosNpc.Sort((a, b) => string.Compare(a.name, b.name));
    }

    void PrepararModelosProduto()
    {
        modelosProduto.Clear();

        AdicionarModeloProduto(modeloProduto);

        if (produtosExtras != null)
        {
            for (int i = 0; i < produtosExtras.Length; i++)
            {
                AdicionarModeloProduto(produtosExtras[i]);
            }
        }

        if (raizProdutosInteragiveis != null)
        {
            Pickup[] pickupsInteragiveis = raizProdutosInteragiveis.GetComponentsInChildren<Pickup>(true);
            for (int i = 0; i < pickupsInteragiveis.Length; i++)
            {
                // Todos os itens reais dentro de Interactables tambem viram modelos possiveis do NPC.
                AdicionarModeloProduto(pickupsInteragiveis[i]);
            }
        }

        if (modelosProduto.Count == 0)
        {
            Pickup[] pickupsDaCena = Resources.FindObjectsOfTypeAll<Pickup>();
            for (int i = 0; i < pickupsDaCena.Length; i++)
            {
                Pickup pickupDaCena = pickupsDaCena[i];
                if (!pickupDaCena.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (EstaDentroDosProdutosVisuaisDaLoja(pickupDaCena.transform))
                {
                    // Ignora os produtos so de fachada quando estamos procurando os interagiveis reais.
                    continue;
                }

                AdicionarModeloProduto(pickupDaCena);
            }
        }

        // Ordena para manter o pareamento previsivel com os NPCs.
        modelosProduto.Sort((a, b) => string.Compare(a.name, b.name));
    }

    void PrepararProdutosDaLoja()
    {
        // Monta a lista de produtos que os NPCs podem escolher durante a compra.
        produtosDaLoja.Clear();

        if (raizProdutosInteragiveis != null)
        {
            Pickup[] pickupsInteragiveis = raizProdutosInteragiveis.GetComponentsInChildren<Pickup>(true);
            for (int i = 0; i < pickupsInteragiveis.Length; i++)
            {
                AdicionarProdutoInteragivelDaLoja(pickupsInteragiveis[i]);
            }
        }

        if (produtosDaLoja.Count > 0)
        {
            return;
        }

        if (raizProdutosVisuaisLoja != null)
        {
            Pickup[] pickupsVisuais = raizProdutosVisuaisLoja.GetComponentsInChildren<Pickup>(true);
            for (int i = 0; i < pickupsVisuais.Length; i++)
            {
                AdicionarProdutoVisualDaLoja(pickupsVisuais[i]);
            }

            return;
        }

        Pickup[] pickupsDaLoja = Resources.FindObjectsOfTypeAll<Pickup>();
        for (int i = 0; i < pickupsDaLoja.Length; i++)
        {
            Pickup pickupDaLoja = pickupsDaLoja[i];
            if (pickupDaLoja == null)
            {
                continue;
            }

            if (!pickupDaLoja.gameObject.scene.IsValid())
            {
                continue;
            }

            if (EstaDentroDeInteractables(pickupDaLoja.transform))
            {
                AdicionarProdutoInteragivelDaLoja(pickupDaLoja);
                continue;
            }

            AdicionarProdutoVisualDaLoja(pickupDaLoja);
        }
    }

    void AdicionarProdutoVisualDaLoja(Pickup pickupDaLoja)
    {
        if (pickupDaLoja == null)
        {
            return;
        }

        if (!ProdutoDaLojaPodeSerEscolhido(pickupDaLoja))
        {
            return;
        }

        // Produtos visuais ficam na prateleira apenas para o NPC parecer visitar a loja.
        pickupDaLoja.ConfigurarComoProdutoVisualDaLoja(pickupDaLoja.ObterRaizProduto());

        if (!produtosDaLoja.Contains(pickupDaLoja))
        {
            produtosDaLoja.Add(pickupDaLoja);
        }
    }

    void AdicionarProdutoInteragivelDaLoja(Pickup pickupDaLoja)
    {
        if (pickupDaLoja == null)
        {
            return;
        }

        // Interactables continuam clicaveis pelo jogador, mas tambem servem de alvo para o NPC.
        pickupDaLoja.ConfigurarComoProdutoDaLojaInteragivel(pickupDaLoja.ObterRaizProduto());

        if (!ProdutoJaEstaNaListaDaLoja(pickupDaLoja))
        {
            produtosDaLoja.Add(pickupDaLoja);
        }
    }

    void PrepararPontosCaminhoAutomatico()
    {
        pontosCaminhoAutomatico.Clear();

        // Junta todos os pontos ja existentes no projeto em um unico pool reutilizavel.
        AdicionarPontosAoPoolAutomatico(caminhoEntrada);
        AdicionarPontosAoPoolAutomatico(caminhoSaida);
        AdicionarRotasAoPoolAutomatico(rotasEntradaAleatorias);
        AdicionarRotasAoPoolAutomatico(rotasSaidaAleatorias);
        AdicionarConfiguracoesAoPoolAutomatico();

        pontosCaminhoAutomatico.RemoveAll(ponto => ponto == null || ponto == spawnNPC || ponto == despawnNPC || ponto == pontoParadaCaixa);
    }

    void AdicionarModeloNpc(GameObject modelo)
    {
        if (modelo == null || modelosNpc.Contains(modelo))
        {
            return;
        }

        if (modelo.scene.IsValid())
        {
            // Mantem o objeto-base da cena escondido para servir apenas como molde.
            modelo.SetActive(false);
        }

        modelosNpc.Add(modelo);
    }

    void AdicionarModeloProduto(Pickup modelo)
    {
        if (modelo == null || ProdutoJaEstaNaListaDeModelos(modelo))
        {
            return;
        }

        modelosProduto.Add(modelo);
    }

    int SortearIndiceNpc()
    {
        if (modelosNpc.Count <= 1)
        {
            return 0;
        }

        int indiceSorteado = Random.Range(0, modelosNpc.Count);

        if (!evitarRepetirNpcEmSequencia)
        {
            return indiceSorteado;
        }

        int tentativas = 0;
        while (indiceSorteado == ultimoIndiceNpc && tentativas < 10)
        {
            indiceSorteado = Random.Range(0, modelosNpc.Count);
            tentativas++;
        }

        return indiceSorteado;
    }

    Pickup ObterProdutoDoNpc(int indiceNpc)
    {
        if (produtosDaLoja.Count > 0)
        {
            return SortearProdutoDaLoja();
        }

        if (modelosProduto.Count == 0)
        {
            return SortearProdutoDaLoja();
        }

        // Usa o pareamento por ordem para o modo legado.
        int indiceProduto = indiceNpc % modelosProduto.Count;
        return modelosProduto[indiceProduto];
    }

    Transform ObterPontoSpawnCliente()
    {
        List<Transform> pontosValidos = new List<Transform>();

        if (pontosSpawn != null)
        {
            for (int i = 0; i < pontosSpawn.Length; i++)
            {
                if (pontosSpawn[i] != null)
                {
                    pontosValidos.Add(pontosSpawn[i]);
                }
            }
        }

        if (pontosValidos.Count > 0)
        {
            // Permite variar o ponto de entrada quando houver mais de um cadastrado.
            return pontosValidos[Random.Range(0, pontosValidos.Count)];
        }

        return spawnNPC;
    }

    Pickup SortearProdutoDaLoja()
    {
        // Sorteia um produto disponivel da loja para o cliente comprar.
        List<Pickup> produtosDisponiveis = new List<Pickup>();

        for (int i = 0; i < produtosDaLoja.Count; i++)
        {
            Pickup produtoDaLoja = produtosDaLoja[i];
            if (ProdutoDaLojaEstaDisponivel(produtoDaLoja) && ProdutoDaLojaPodeSerEscolhido(produtoDaLoja))
            {
                produtosDisponiveis.Add(produtoDaLoja);
            }
        }

        if (produtosDisponiveis.Count == 0)
        {
            return null;
        }

        bool usandoInteractables = ExistemProdutosInteragiveis(produtosDisponiveis);
        if (!usandoInteractables)
        {
            OrdenarProdutosPorProximidadeDoCaixa(produtosDisponiveis);
        }

        int quantidadeConsiderada = usandoInteractables
            ? produtosDisponiveis.Count
            : Mathf.Clamp(quantidadeProdutosMaisProximos, 1, produtosDisponiveis.Count);
        return produtosDisponiveis[Random.Range(0, quantidadeConsiderada)];
    }

    Pickup SpawnarProdutoAtual()
    {
        if (modeloProdutoAtual == null || spawnProduto == null)
        {
            return null;
        }

        // O item aparece apenas quando o cliente ja esta posicionado no caixa.
        return CriarCopiaDoProdutoNoCaixa(modeloProdutoAtual);
    }

    void CriarProdutoCarregadoPeloNpc()
    {
        // Cria uma copia visual do produto para parecer que o NPC esta carregando o item.
        if (npcAtual == null || produtoCarregadoAtual != null)
        {
            return;
        }

        Pickup produtoBase = produtoDestinoAtual != null ? produtoDestinoAtual : modeloProdutoAtual;
        Transform raizModelo = produtoBase != null ? produtoBase.ObterRaizProduto() : null;
        if (raizModelo == null)
        {
            return;
        }

        escalaOriginalProdutoCarregadoAtual = raizModelo.localScale;

        GameObject novoObjeto = Instantiate(raizModelo.gameObject);
        novoObjeto.name = raizModelo.name + "_CarregadoNPC";
        novoObjeto.SetActive(true);
        AtivarObjetoInteiro(novoObjeto.transform);

        Transform pontoCarregar = ObterPontoCarregarProdutoNpc();
        novoObjeto.transform.SetParent(pontoCarregar, false);
        novoObjeto.transform.localPosition = offsetProdutoCarregadoNpc;
        novoObjeto.transform.localRotation = Quaternion.identity;
        novoObjeto.transform.localScale = escalaOriginalProdutoCarregadoAtual;
        RedimensionarProdutoCarregado(novoObjeto.transform);

        produtoCarregadoAtual = novoObjeto.GetComponentInChildren<Pickup>(true);
        if (produtoCarregadoAtual != null)
        {
            produtoCarregadoAtual.ConfigurarComoProdutoDoCaixa(novoObjeto.transform);
            produtoCarregadoAtual.IgnorarScannerPorSegundos(9999f);
        }

        AjustarProdutoCarregadoVisual(novoObjeto.transform);
        objetoProdutoCarregadoAtual = novoObjeto;
    }

    Transform ObterPontoCarregarProdutoNpc()
    {
        return npcAtual != null ? npcAtual.transform : transform;
    }

    void AjustarProdutoCarregadoVisual(Transform raiz)
    {
        if (raiz == null)
        {
            return;
        }

        Collider[] colliders = raiz.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = raiz.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].linearVelocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
            rigidbodies[i].detectCollisions = false;
        }
    }

    Pickup ColocarProdutoCarregadoNoCaixa()
    {
        // Move o produto que o NPC estava carregando para o ponto do checkout.
        if (produtoCarregadoAtual == null || objetoProdutoCarregadoAtual == null)
        {
            return SpawnarProdutoAtual();
        }

        objetoProdutoCarregadoAtual.transform.SetParent(null);
        objetoProdutoCarregadoAtual.transform.position = CalcularPosicaoSpawnProdutoNoCaixa();
        objetoProdutoCarregadoAtual.transform.rotation = spawnProduto != null ? spawnProduto.rotation : Quaternion.identity;
        objetoProdutoCarregadoAtual.transform.localScale = escalaOriginalProdutoCarregadoAtual;

        AtivarObjetoInteiro(objetoProdutoCarregadoAtual.transform);
        produtoCarregadoAtual.ConfigurarComoProdutoDoCaixa(objetoProdutoCarregadoAtual.transform);
        produtoCarregadoAtual.IgnorarScannerPorSegundos(tempoIgnorarScannerAoSpawnar);
        AjustarFisicaDoProdutoClonado(objetoProdutoCarregadoAtual.transform, produtoCarregadoAtual);

        Pickup produtoNoCaixa = produtoCarregadoAtual;
        produtoCarregadoAtual = null;
        objetoProdutoCarregadoAtual = null;
        escalaOriginalProdutoCarregadoAtual = Vector3.one;
        return produtoNoCaixa;
    }

    void RedimensionarProdutoCarregado(Transform raiz)
    {
        if (raiz == null)
        {
            return;
        }

        float escalaBase = Mathf.Clamp(escalaProdutoCarregadoNpc, 0.05f, 1f);
        raiz.localScale = escalaOriginalProdutoCarregadoAtual * escalaBase;

        Bounds boundsProduto;
        if (!TentarObterBoundsRenderers(raiz, out boundsProduto))
        {
            return;
        }

        float maiorEixo = Mathf.Max(boundsProduto.size.x, boundsProduto.size.y, boundsProduto.size.z);
        if (maiorEixo <= tamanhoMaximoProdutoCarregadoNpc || maiorEixo <= 0.0001f)
        {
            return;
        }

        float fatorReducao = tamanhoMaximoProdutoCarregadoNpc / maiorEixo;
        raiz.localScale *= fatorReducao;
    }

    bool TentarObterBoundsRenderers(Transform raiz, out Bounds boundsProduto)
    {
        boundsProduto = new Bounds(raiz.position, Vector3.zero);
        Renderer[] renderers = raiz.GetComponentsInChildren<Renderer>(true);
        bool encontrouRenderer = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null || !rendererAtual.enabled)
            {
                continue;
            }

            if (!encontrouRenderer)
            {
                boundsProduto = rendererAtual.bounds;
                encontrouRenderer = true;
            }
            else
            {
                boundsProduto.Encapsulate(rendererAtual.bounds);
            }
        }

        return encontrouRenderer;
    }

    Pickup CriarCopiaDoProdutoNoCaixa(Pickup produtoBase)
    {
        // Fallback: cria o produto diretamente no caixa quando nao existe item carregado.
        Transform raizModelo = produtoBase.ObterRaizProduto();
        if (raizModelo == null)
        {
            return null;
        }

        Vector3 posicaoSpawnProduto = CalcularPosicaoSpawnProdutoNoCaixa();

        // Clona a raiz inteira do item para suportar modelos com varios filhos.
        GameObject novoObjeto = Instantiate(raizModelo.gameObject, posicaoSpawnProduto, spawnProduto.rotation);
        novoObjeto.name = raizModelo.name;
        novoObjeto.SetActive(true);
        AtivarObjetoInteiro(novoObjeto.transform);

        Pickup novoProduto = novoObjeto.GetComponentInChildren<Pickup>(true);
        if (novoProduto == null)
        {
            Debug.LogWarning("O produto clonado nao possui Pickup em nenhum child.", novoObjeto);
            Destroy(novoObjeto);
            return null;
        }

        // Marca o clone como produto do caixa e protege contra autoescaneamento instantaneo.
        novoProduto.ConfigurarComoProdutoDoCaixa(novoObjeto.transform);
        novoProduto.IgnorarScannerPorSegundos(tempoIgnorarScannerAoSpawnar);
        AjustarFisicaDoProdutoClonado(novoObjeto.transform, novoProduto);
        return novoProduto;
    }

    void AtivarObjetoInteiro(Transform raiz)
    {
        if (raiz == null)
        {
            return;
        }

        // Reativa toda a hierarquia para o clone ficar visivel por completo.
        raiz.gameObject.SetActive(true);

        for (int i = 0; i < raiz.childCount; i++)
        {
            AtivarObjetoInteiro(raiz.GetChild(i));
        }

        Renderer[] renderers = raiz.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }

        Collider[] colliders = raiz.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
        }
    }

    void AjustarFisicaDoProdutoClonado(Transform raiz, Pickup pickupClonado)
    {
        if (raiz == null)
        {
            return;
        }

        Rigidbody[] rigidbodies = raiz.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            // Mantem o clone parado e estavel no checkout ate o jogador pegar.
            if (!rigidbodies[i].isKinematic)
            {
                rigidbodies[i].linearVelocity = Vector3.zero;
                rigidbodies[i].angularVelocity = Vector3.zero;
            }

            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }

        if (pickupClonado != null)
        {
            pickupClonado.FixarNoCaixa();
        }
    }

    Vector3 CalcularPosicaoSpawnProdutoNoCaixa()
    {
        if (spawnProduto == null)
        {
            return Vector3.zero;
        }

        if (scanner == null)
        {
            return spawnProduto.position;
        }

        Vector3 posicaoBase = spawnProduto.position;
        Vector3 deslocamentoBase = posicaoBase - scanner.transform.position;
        deslocamentoBase.y = 0f;

        if (deslocamentoBase.sqrMagnitude < 0.0001f)
        {
            deslocamentoBase = ObterDirecaoLateralDoCaixa();
        }

        Vector3 posicaoCorrigida = scanner.transform.position + deslocamentoBase.normalized * Mathf.Max(distanciaMinimaSpawnProdutoDoScanner, deslocamentoBase.magnitude);
        posicaoCorrigida.y = posicaoBase.y;
        return posicaoCorrigida;
    }

    Vector3 ObterDirecaoLateralDoCaixa()
    {
        Vector3 direcaoCorredor = despawnNPC != null && spawnNPC != null ? despawnNPC.position - spawnNPC.position : Vector3.right;
        direcaoCorredor.y = 0f;

        if (direcaoCorredor.sqrMagnitude < 0.0001f)
        {
            direcaoCorredor = Vector3.right;
        }

        Vector3 ladoA = Vector3.Cross(Vector3.up, direcaoCorredor).normalized;
        Vector3 ladoB = -ladoA;
        Vector3 referenciaSpawn = spawnProduto != null ? spawnProduto.position - scanner.transform.position : ladoA;
        referenciaSpawn.y = 0f;

        // Escolhe o lado que mais combina com a posicao original do SpawnObjects na cena.
        return Vector3.Dot(referenciaSpawn, ladoA) >= Vector3.Dot(referenciaSpawn, ladoB) ? ladoA : ladoB;
    }

    Vector3 CalcularPontoParadaNoCaixa()
    {
        if (pontoParadaCaixa != null)
        {
            // Usa o waypoint manual de parada quando ele existir na cena.
            Vector3 pontoManual = pontoParadaCaixa.position;
            if (spawnNPC != null)
            {
                pontoManual.y = spawnNPC.position.y;
            }

            return pontoManual;
        }

        if (scanner != null)
        {
            // Fallback simples caso o waypoint manual nao exista.
            return new Vector3(scanner.transform.position.x, spawnNPC != null ? spawnNPC.position.y : scanner.transform.position.y, scanner.transform.position.z);
        }

        return Vector3.zero;
    }

    Vector3 ObterPosicaoProdutoDestinoAtual()
    {
        if (possuiPosicaoParadaProdutoAtual)
        {
            return posicaoParadaProdutoAtual;
        }

        if (produtoDestinoAtual == null)
        {
            return CalcularPontoParadaNoCaixa();
        }

        Transform raizProdutoDestino = produtoDestinoAtual.ObterRaizProduto();
        Vector3 posicaoProduto = raizProdutoDestino != null ? raizProdutoDestino.position : produtoDestinoAtual.transform.position;
        posicaoProduto.y = npcAtual != null ? npcAtual.transform.position.y : posicaoProduto.y;
        return posicaoProduto;
    }

    Vector3 CalcularPontoParadaNoProduto(Pickup produto, Vector3 referenciaAproximacao)
    {
        if (produto == null)
        {
            return referenciaAproximacao;
        }

        Transform raizProduto = produto.ObterRaizProduto();
        Vector3 posicaoProduto = raizProduto != null ? raizProduto.position : produto.transform.position;
        posicaoProduto.y = spawnNPC != null ? spawnNPC.position.y : posicaoProduto.y;

        Vector3 referenciaPlana = referenciaAproximacao;
        referenciaPlana.y = posicaoProduto.y;
        Vector3 direcaoAproximacao = posicaoProduto - referenciaPlana;
        direcaoAproximacao.y = 0f;

        if (direcaoAproximacao.sqrMagnitude < 0.0001f)
        {
            direcaoAproximacao = pontoParadaCaixa != null
                ? posicaoProduto - pontoParadaCaixa.position
                : Vector3.forward;
            direcaoAproximacao.y = 0f;
        }

        if (direcaoAproximacao.sqrMagnitude < 0.0001f)
        {
            direcaoAproximacao = Vector3.forward;
        }

        float distanciaSegura = Mathf.Max(distanciaParadaNoProduto, raioColisaoNpc + 0.35f);
        Vector3 pontoParada = posicaoProduto - direcaoAproximacao.normalized * distanciaSegura;
        pontoParada.y = posicaoProduto.y;
        return pontoParada;
    }

    bool JogadorPertoDoScanner()
    {
        if (jogador == null || scanner == null)
        {
            return true;
        }

        // Ignora a altura para medir apenas a distancia horizontal ate o caixa.
        Vector3 posicaoJogador = jogador.position;
        Vector3 posicaoScanner = scanner.transform.position;
        posicaoJogador.y = 0f;
        posicaoScanner.y = 0f;
        return Vector3.Distance(posicaoJogador, posicaoScanner) <= distanciaEntregaTroco;
    }

    float SortearValorPago(float preco)
    {
        List<float> valoresValidos = new List<float>();

        for (int i = 0; i < valoresPagamento.Length; i++)
        {
            if (valoresPagamento[i] >= preco)
            {
                valoresValidos.Add(valoresPagamento[i]);
            }
        }

        if (valoresValidos.Count == 0)
        {
            // Cria um fallback simples quando nenhum valor configurado cobre a compra.
            return preco + 10f;
        }

        return valoresValidos[Random.Range(0, valoresValidos.Count)];
    }

    string FormatarDinheiro(float valor)
    {
        // Usa duas casas e virgula para combinar com o formato brasileiro.
        return valor.ToString("F2").Replace(".", ",");
    }

    float ObterValorTrocoDigitado()
    {
        // Converte o texto digitado em centavos para um valor decimal em reais.
        long centavos = 0;
        long.TryParse(trocoDigitadoEmCentavos, out centavos);
        return centavos / 100f;
    }

    string FormatarTrocoDigitado()
    {
        return FormatarDinheiro(ObterValorTrocoDigitado());
    }

    Pickup ObterProdutoDaLojaCorrespondente(Pickup produtoModelo)
    {
        if (produtosDaLoja.Count == 0)
        {
            return null;
        }

        if (produtoModelo == null)
        {
            return SortearProdutoDaLoja();
        }

        string identificadorModelo = NormalizarIdentificadorProduto(produtoModelo);
        List<Pickup> correspondentes = new List<Pickup>();

        for (int i = 0; i < produtosDaLoja.Count; i++)
        {
            Pickup produtoDaLoja = produtosDaLoja[i];
            if (!ProdutoDaLojaEstaDisponivel(produtoDaLoja))
            {
                continue;
            }

            if (!ProdutoDaLojaPodeSerEscolhido(produtoDaLoja))
            {
                continue;
            }

            if (NormalizarIdentificadorProduto(produtoDaLoja) == identificadorModelo)
            {
                correspondentes.Add(produtoDaLoja);
            }
        }

        if (correspondentes.Count > 0)
        {
            // Quando houver mais de uma copia do mesmo produto, usa a mais proxima do caixa.
            OrdenarProdutosPorProximidadeDoCaixa(correspondentes);
            return correspondentes[0];
        }

        return SortearProdutoDaLoja();
    }

    bool ProdutoDaLojaEstaDisponivel(Pickup produtoDaLoja)
    {
        if (produtoDaLoja == null)
        {
            return false;
        }

        Transform raizProduto = produtoDaLoja.ObterRaizProduto();
        return raizProduto != null && raizProduto.gameObject.activeInHierarchy;
    }

    bool ProdutoDaLojaPodeSerEscolhido(Pickup produtoDaLoja)
    {
        if (produtoDaLoja == null)
        {
            return false;
        }

        Transform raizProduto = produtoDaLoja.ObterRaizProduto();
        if (raizProduto == null)
        {
            return false;
        }

        if (raizProdutosInteragiveis != null && raizProduto.IsChildOf(raizProdutosInteragiveis))
        {
            return true;
        }

        if (raizProdutosAtendimentoNpc != null && !raizProduto.IsChildOf(raizProdutosAtendimentoNpc))
        {
            // Se existir uma raiz dedicada ao atendimento, o NPC so escolhe produto dali.
            return false;
        }

        if (!limitarProdutosPelaDistanciaAoCaixa)
        {
            return ProdutoTemRotaCompletaPeloNavMesh(produtoDaLoja);
        }

        return DistanciaPlana(raizProduto.position, CalcularPontoParadaNoCaixa()) <= distanciaMaximaProdutoDoCaixa
            && ProdutoTemRotaCompletaPeloNavMesh(produtoDaLoja);
    }

    bool ProdutoTemRotaCompletaPeloNavMesh(Pickup produtoDaLoja)
    {
        if (!usarNavMesh || !navMeshPronto)
        {
            return true;
        }

        Transform pontoSpawn = ObterPontoSpawnCliente();
        if (pontoSpawn == null || produtoDaLoja == null)
        {
            return false;
        }

        Vector3 pontoProduto = CalcularPontoParadaNoProduto(produtoDaLoja, pontoSpawn.position);
        return ExisteCaminhoNavMeshEntrePontos(pontoSpawn.position, pontoProduto)
            && ExisteCaminhoNavMeshEntrePontos(pontoProduto, CalcularPontoParadaNoCaixa());
    }

    void OrdenarProdutosPorProximidadeDoCaixa(List<Pickup> produtosOrdenados)
    {
        Vector3 posicaoCaixa = CalcularPontoParadaNoCaixa();
        produtosOrdenados.Sort((produtoA, produtoB) =>
        {
            float distanciaA = produtoA == null ? float.MaxValue : DistanciaPlana(produtoA.ObterRaizProduto().position, posicaoCaixa);
            float distanciaB = produtoB == null ? float.MaxValue : DistanciaPlana(produtoB.ObterRaizProduto().position, posicaoCaixa);
            return distanciaA.CompareTo(distanciaB);
        });
    }

    string NormalizarIdentificadorProduto(Pickup produto)
    {
        if (produto == null)
        {
            return string.Empty;
        }

        string textoBase = produto.ObterCodigoProduto();
        if (string.IsNullOrWhiteSpace(textoBase))
        {
            textoBase = produto.ObterNomeProduto();
        }

        return textoBase.Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }

    bool EstaDentroDeInteractables(Transform item)
    {
        return raizProdutosInteragiveis != null && item != null && item.IsChildOf(raizProdutosInteragiveis);
    }

    bool ProdutoJaEstaNaListaDaLoja(Pickup produto)
    {
        if (produto == null)
        {
            return true;
        }

        Transform raizProduto = produto.ObterRaizProduto();
        for (int i = 0; i < produtosDaLoja.Count; i++)
        {
            Pickup produtoExistente = produtosDaLoja[i];
            if (produtoExistente == null)
            {
                continue;
            }

            if (produtoExistente == produto || produtoExistente.ObterRaizProduto() == raizProduto)
            {
                return true;
            }
        }

        return false;
    }

    bool ProdutoJaEstaNaListaDeModelos(Pickup produto)
    {
        Transform raizProduto = produto.ObterRaizProduto();
        for (int i = 0; i < modelosProduto.Count; i++)
        {
            Pickup produtoExistente = modelosProduto[i];
            if (produtoExistente == null)
            {
                continue;
            }

            if (produtoExistente == produto || produtoExistente.ObterRaizProduto() == raizProduto)
            {
                return true;
            }
        }

        return false;
    }

    bool ExistemProdutosInteragiveis(List<Pickup> produtos)
    {
        for (int i = 0; i < produtos.Count; i++)
        {
            Pickup produto = produtos[i];
            if (produto != null && EstaDentroDeInteractables(produto.ObterRaizProduto()))
            {
                return true;
            }
        }

        return false;
    }

    bool EstaDentroDosProdutosVisuaisDaLoja(Transform item)
    {
        return raizProdutosVisuaisLoja != null && item != null && item.IsChildOf(raizProdutosVisuaisLoja);
    }

    void AdicionarPontosAoPoolAutomatico(Transform[] pontos)
    {
        if (pontos == null)
        {
            return;
        }

        for (int i = 0; i < pontos.Length; i++)
        {
            Transform ponto = pontos[i];
            if (ponto != null && !pontosCaminhoAutomatico.Contains(ponto))
            {
                pontosCaminhoAutomatico.Add(ponto);
            }
        }
    }

    void AdicionarRotasAoPoolAutomatico(RotaWaypoint[] rotas)
    {
        if (rotas == null)
        {
            return;
        }

        for (int i = 0; i < rotas.Length; i++)
        {
            if (rotas[i] != null)
            {
                AdicionarPontosAoPoolAutomatico(rotas[i].pontos);
            }
        }
    }

    void AdicionarConfiguracoesAoPoolAutomatico()
    {
        if (configuracoesClientes == null)
        {
            return;
        }

        for (int i = 0; i < configuracoesClientes.Length; i++)
        {
            ConfiguracaoClienteCaixa configuracao = configuracoesClientes[i];
            if (configuracao == null)
            {
                continue;
            }

            AdicionarPontosAoPoolAutomatico(configuracao.caminhoEntrada);
            AdicionarPontosAoPoolAutomatico(configuracao.caminhoSaida);
        }
    }

    float DistanciaPlana(Vector3 origem, Vector3 destino)
    {
        origem.y = 0f;
        destino.y = 0f;
        return Vector3.Distance(origem, destino);
    }

    void LimparAtendimentoAtual()
    {
        // Remove sobras do atendimento atual antes de iniciar outro cliente.
        if (agenteNpcAtual != null && agenteNpcAtual.enabled && agenteNpcAtual.isOnNavMesh)
        {
            // Interrompe o deslocamento atual antes de destruir o clone.
            agenteNpcAtual.ResetPath();
        }

        if (npcAtual != null)
        {
            Destroy(npcAtual);
            npcAtual = null;
        }

        if (produtoAtual != null)
        {
            // Remove a raiz inteira do clone para nao deixar sobras invisiveis no caixa.
            Transform raizProdutoAtual = produtoAtual.ObterRaizProduto();
            Destroy(raizProdutoAtual != null ? raizProdutoAtual.gameObject : produtoAtual.gameObject);
            produtoAtual = null;
        }

        if (objetoProdutoCarregadoAtual != null)
        {
            Destroy(objetoProdutoCarregadoAtual);
            objetoProdutoCarregadoAtual = null;
            produtoCarregadoAtual = null;
        }

        trocoDigitadoEmCentavos = string.Empty;
        mensagemTrocoAtual = string.Empty;
        avisoTrocoAtual = string.Empty;
        configuracaoAtual = null;
        produtoDestinoAtual = null;
        modeloProdutoAtual = null;
        agenteNpcAtual = null;
        velocidadeNpcAtual = 0f;
        ultimaDirecaoMovimentoNpc = Vector3.forward;
        possuiPosicaoParadaProdutoAtual = false;
        posicaoParadaProdutoAtual = Vector3.zero;
        ultimoDestinoNavMesh = Vector3.zero;
        destinoNavMeshDefinido = false;
        caminhoProdutoAtual.Clear();
        caminhoEntradaAtual.Clear();
        caminhoSaidaAtual.Clear();
        indiceWaypointProdutoAtual = 0;
        indiceWaypointEntradaAtual = 0;
        indiceWaypointSaidaAtual = 0;
    }
}
