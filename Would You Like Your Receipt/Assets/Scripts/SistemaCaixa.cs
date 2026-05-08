using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConfiguracaoClienteCaixa
{
    // Nome opcional para identificar esta variacao no Inspector.
    public string nomeVariacao = "Cliente";
    // Modelo do NPC usado nesta variacao.
    public GameObject npc;
    // Produto que este NPC leva ate o caixa.
    public Pickup produto;
    // Caminho de entrada definido por Empty Objects espalhados pelo mercado.
    public Transform[] caminhoEntrada;
    // Caminho de saida definido por Empty Objects ate a porta do mercado.
    public Transform[] caminhoSaida;
}

public class SistemaCaixa : MonoBehaviour
{
    // Estados do fluxo do cliente no caixa.
    enum EstadoCliente
    {
        AguardandoProximoCliente,
        IndoAoProduto,
        IndoAoCaixa,
        AguardandoEscaneamento,
        AguardandoTroco,
        IndoEmbora
    }

    // Referencias principais do sistema.
    [SerializeField] Scanner scanner;
    [SerializeField] GoalManager goalManager;
    [SerializeField] Transform jogador;

    // Modelos legados que ja existem na cena.
    [SerializeField] GameObject npcCliente;
    [SerializeField] Pickup modeloProduto;
    [SerializeField] Transform spawnProduto;

    // Campos extras para cadastrar mais NPCs e produtos no Inspector.
    [SerializeField] GameObject[] npcsExtras;
    [SerializeField] Pickup[] produtosExtras;
    // Lista principal de variacoes. Cada entrada pode ter NPC, produto e caminho proprios.
    [SerializeField] ConfiguracaoClienteCaixa[] configuracoesClientes = new ConfiguracaoClienteCaixa[8];

    // Pontos de apoio para o fluxo de entrada e saida.
    [SerializeField] Transform[] caminhoEntrada;
    [SerializeField] Transform[] caminhoSaida;
    [SerializeField] Transform[] pontosSpawn;
    [SerializeField] Transform spawnNPC;
    [SerializeField] Transform despawnNPC;
    [SerializeField] Transform interactables;
    [SerializeField] Transform storeModel;

    // Ajustes de comportamento.
    [SerializeField] float velocidadeNpc = 2.2f;
    [SerializeField] float tempoEntreClientes = 4f;
    [SerializeField] float distanciaEntregaTroco = 2.5f;
    [SerializeField] float distanciaChegadaWaypoint = 0.08f;
    [SerializeField] float distanciaChegadaProduto = 0.45f;
    [SerializeField] bool pegarProdutosDaPrateleira = true;
    [SerializeField] float raioAgenteNpc = 0.35f;
    [SerializeField] float alturaAgenteNpc = 1.8f;
    [SerializeField] float distanciaAproximacaoProduto = 1.1f;
    [SerializeField] bool sortearCaminhoEntrada = true;
    [SerializeField] bool sortearCaminhoSaida = true;
    [SerializeField] float[] valoresPagamento = { 20f, 50f, 100f };
    [SerializeField] bool evitarRepetirNpcEmSequencia = true;
    [SerializeField] int maximoDigitosTroco = 6;

    // Listas auxiliares montadas em runtime.
    readonly List<GameObject> modelosNpc = new List<GameObject>();
    readonly List<Pickup> modelosProduto = new List<Pickup>();
    readonly List<Transform> caminhoEntradaAtual = new List<Transform>();
    readonly List<Transform> caminhoSaidaAtual = new List<Transform>();
    readonly List<int> indicesConfiguracoesValidas = new List<int>();

    // Estado atual do sistema.
    EstadoCliente estadoAtual = EstadoCliente.AguardandoProximoCliente;

    // Referencias do atendimento atual.
    GameObject npcAtual;
    Pickup produtoAtual;
    Pickup modeloProdutoAtual;
    Pickup produtoDaPrateleiraAtual;
    GameObject produtoCaixaPendente;
    Pickup pickupProdutoCaixaPendente;
    Transform raizProdutoDaPrateleiraAtual;
    ConfiguracaoClienteCaixa configuracaoAtual;

    // Indices usados para evitar repeticao e andar pelos waypoints.
    int ultimoIndiceNpc = -1;
    int ultimoIndiceConfiguracao = -1;
    int indiceWaypointEntradaAtual;
    int indiceWaypointSaidaAtual;

    // Dados financeiros do atendimento atual.
    float totalCompra;
    float valorPago;
    float troco;

    // Diz se o sistema encontrou tudo que precisava para funcionar.
    bool sistemaPronto;

    // Estado da digitacao do troco.
    string trocoDigitadoEmCentavos = string.Empty;
    string mensagemTrocoAtual = string.Empty;
    string avisoTrocoAtual = string.Empty;

    private void OnValidate()
    {
        // Mantem oito espacos prontos no Inspector para facilitar montar as variacoes.
        if (configuracoesClientes == null || configuracoesClientes.Length < 8)
        {
            System.Array.Resize(ref configuracoesClientes, 8);
        }
    }

    private void OnEnable()
    {
        // Se inscreve no evento do scanner para reagir quando um produto for lido.
        Scanner.ProdutoEscaneado += AoProdutoEscaneado;
    }

    private void OnDisable()
    {
        // Remove a inscricao para evitar chamadas indevidas em objetos destruidos.
        Scanner.ProdutoEscaneado -= AoProdutoEscaneado;
    }

    private void Awake()
    {
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

        // Reaproveita os pontos ja presentes na cena quando os campos novos estiverem vazios.
        PrepararPontosDaCena();
        PrepararInteractables();
        PrepararProdutosDaPrateleira();
        PrepararStoreModel();
        PrepararProdutosDoEstoque();
        PrepararColisaoDaLoja();

        // Monta as listas de fallback a partir do Inspector e do que ja existe na cena.
        PrepararModelosNpc();
        PrepararModelosProduto();

        // Prepara as configuracoes completas de cliente e suas rotas.
        PrepararConfiguracoesClientes();
        PrepararIndicesConfiguracoesValidas();

        if (scanner == null || spawnNPC == null || despawnNPC == null || spawnProduto == null)
        {
            Debug.LogWarning("SistemaCaixa nao encontrou Scanner, SpawnNPC, DespawnNPC ou SpawnObjects.", this);
            enabled = false;
            return;
        }

        bool podeUsarPrateleira = pegarProdutosDaPrateleira && interactables != null;
        if (indicesConfiguracoesValidas.Count == 0 && modelosNpc.Count == 0)
        {
            Debug.LogWarning("SistemaCaixa precisa de pelo menos um NPC configurado.", this);
            enabled = false;
            return;
        }

        if (!podeUsarPrateleira && indicesConfiguracoesValidas.Count == 0 && modelosProduto.Count == 0)
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
        if (sistemaPronto)
        {
            StartCoroutine(RotinaClientes());
        }
    }

    private void Update()
    {
        if (!sistemaPronto)
        {
            return;
        }

        if (estadoAtual == EstadoCliente.IndoAoProduto)
        {
            // Faz o NPC andar ate o produto escolhido na prateleira.
            MoverNpcParaProduto();
        }
        else if (estadoAtual == EstadoCliente.IndoAoCaixa)
        {
            // Faz o NPC seguir os pontos de entrada antes de parar na frente do caixa.
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
            // Faz o NPC seguir os pontos de saida antes de desaparecer na porta.
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
        // Loop principal: espera um tempo, cria um cliente e so segue quando ele termina.
        while (true)
        {
            yield return new WaitForSeconds(tempoEntreClientes);
            IniciarAtendimento();

            while (estadoAtual != EstadoCliente.AguardandoProximoCliente)
            {
                yield return null;
            }
        }
    }

    void IniciarAtendimento()
    {
        // Limpa qualquer sobra do atendimento anterior.
        LimparAtendimentoAtual();

        GameObject modeloNpcEscolhido = null;
        modeloProdutoAtual = null;
        produtoDaPrateleiraAtual = null;
        raizProdutoDaPrateleiraAtual = null;
        configuracaoAtual = null;

        // Tenta usar primeiro uma configuracao completa com rota propria.
        if (TentarEscolherConfiguracaoCliente())
        {
            modeloNpcEscolhido = configuracaoAtual.npc;
            modeloProdutoAtual = configuracaoAtual.produto;

            // Mantem exatamente a ordem dos pontos definida para este NPC.
            PrepararCaminhoOrdenado(configuracaoAtual.caminhoEntrada, caminhoEntradaAtual);
            PrepararCaminhoOrdenado(configuracaoAtual.caminhoSaida, caminhoSaidaAtual);
        }
        else
        {
            // Fallback antigo: sorteia NPC e produto separadamente.
            int indiceNpcEscolhido = SortearIndiceNpc();
            modeloNpcEscolhido = modelosNpc[indiceNpcEscolhido];
            modeloProdutoAtual = ObterProdutoDoNpc(indiceNpcEscolhido);

            // Usa os arrays antigos de caminho, podendo embaralhar se desejado.
            PrepararCaminhoAtual(caminhoEntrada, caminhoEntradaAtual, sortearCaminhoEntrada);
            PrepararCaminhoAtual(caminhoSaida, caminhoSaidaAtual, sortearCaminhoSaida);
            ultimoIndiceNpc = indiceNpcEscolhido;
        }

        // Remove spawn e despawn caso eles tenham sido incluidos por engano na lista.
        RemoverPontoDoCaminho(caminhoEntradaAtual, spawnNPC);
        RemoverPontoDoCaminho(caminhoSaidaAtual, despawnNPC);
        indiceWaypointEntradaAtual = 0;
        indiceWaypointSaidaAtual = 0;

        if (pegarProdutosDaPrateleira && TentarEscolherProdutoDaPrateleira())
        {
            // Troca o produto configurado por um item real que esta na aba Interactables.
            modeloProdutoAtual = produtoDaPrateleiraAtual;
            PrepararProdutoPendenteDoCaixa(produtoDaPrateleiraAtual);
        }

        if (modeloNpcEscolhido == null || modeloProdutoAtual == null)
        {
            Debug.LogWarning("SistemaCaixa nao conseguiu escolher um NPC ou produto valido.", this);
            estadoAtual = EstadoCliente.AguardandoProximoCliente;
            return;
        }

        // Zera os valores financeiros do novo atendimento.
        totalCompra = 0f;
        valorPago = 0f;
        troco = 0f;

        // Cria o NPC na entrada do mercado.
        npcAtual = Instantiate(modeloNpcEscolhido, spawnNPC.position, spawnNPC.rotation);
        npcAtual.name = modeloNpcEscolhido.name;
        npcAtual.SetActive(true);

        NPCInteraction interacaoClone = npcAtual.GetComponent<NPCInteraction>();
        if (interacaoClone != null)
        {
            // O cliente do caixa nao usa interacao manual enquanto participa do fluxo.
            interacaoClone.enabled = false;
        }

        estadoAtual = produtoDaPrateleiraAtual != null ? EstadoCliente.IndoAoProduto : EstadoCliente.IndoAoCaixa;
    }

    void MoverNpcParaProduto()
    {
        if (produtoDaPrateleiraAtual == null || raizProdutoDaPrateleiraAtual == null)
        {
            estadoAtual = EstadoCliente.IndoAoCaixa;
            return;
        }

        if (!TentarCalcularPontoAproximacaoProduto(raizProdutoDaPrateleiraAtual, out Vector3 posicaoProduto))
        {
            // Se nao existe ponto alcançavel perto do item, o cliente nao atravessa a loja para roubar caminho.
            return;
        }

        if (!MoverNpcAte(posicaoProduto, distanciaChegadaProduto))
        {
            return;
        }

        // Ao chegar na prateleira, o cliente tira o produto inteiro dali.
        produtoDaPrateleiraAtual.RetirarDaPrateleiraParaNpc();
        estadoAtual = EstadoCliente.IndoAoCaixa;
    }

    void AoChegarNoCaixa()
    {
        // Quando o NPC chega ao caixa, cria o produto dele no ponto de spawn.
        produtoAtual = SpawnarProdutoAtual();

        if (produtoAtual == null)
        {
            scanner.MostrarTextoInfoProduto("Cliente chegou, mas nao havia produto configurado.", 2f);
            indiceWaypointSaidaAtual = 0;
            estadoAtual = EstadoCliente.IndoEmbora;
            return;
        }

        scanner.MostrarTextoInfoProduto(
            "Cliente no caixa.\n\n" +
            produtoAtual.ObterMensagemEscaneamento() +
            "\n\nEscaneie o produto para continuar.",
            0f
        );
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

        // Calcula total, pagamento e troco assim que o item correto e lido.
        totalCompra = pickup.ObterPrecoProduto();
        valorPago = SortearValorPago(totalCompra);
        troco = Mathf.Max(0f, valorPago - totalCompra);

        if (produtoDaPrateleiraAtual != null)
        {
            // Libera o espaco vazio da prateleira para aceitar reposicao vinda do estoque.
            produtoDaPrateleiraAtual.LiberarDepoisDoAtendimento();
        }

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
        if (goalManager != null)
        {
            // Conta o cliente como atendido e mostra confirmacao na tela.
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
            // Remove o NPC da cena quando ele chega ao ponto de saida.
            Destroy(npcAtual);
            npcAtual = null;
        }

        estadoAtual = EstadoCliente.AguardandoProximoCliente;
    }

    void MoverNpcPeloCaminho(List<Transform> caminhoAtual, ref int indiceWaypointAtual, Vector3 destinoFinal, EstadoCliente proximoEstado, System.Action aoChegar)
    {
        if (npcAtual == null)
        {
            estadoAtual = EstadoCliente.AguardandoProximoCliente;
            return;
        }

        // Ignora referencias vazias para nao travar o NPC se algum ponto for apagado.
        while (indiceWaypointAtual < caminhoAtual.Count && caminhoAtual[indiceWaypointAtual] == null)
        {
            indiceWaypointAtual++;
        }

        bool seguindoWaypoint = indiceWaypointAtual < caminhoAtual.Count;
        Vector3 destino = seguindoWaypoint ? caminhoAtual[indiceWaypointAtual].position : destinoFinal;

        if (!MoverNpcAte(destino))
        {
            return;
        }

        if (seguindoWaypoint)
        {
            // Quando chega em um ponto, passa para o proximo antes de mirar o destino final.
            indiceWaypointAtual++;
            return;
        }

        estadoAtual = proximoEstado;
        aoChegar?.Invoke();
    }

    bool MoverNpcAte(Vector3 destino)
    {
        return MoverNpcAte(destino, distanciaChegadaWaypoint);
    }

    bool MoverNpcAte(Vector3 destino, float distanciaChegada)
    {
        // O NPC usa apenas o pathfinding fisico por colisao para manter um comportamento unico.
        return MoverNpcComPathfindingFisico(destino, distanciaChegada);
    }

    bool MoverNpcDiretoPara(Vector3 destino, float distanciaChegada)
    {
        // Este movimento direto so e usado entre pontos ja aprovados pelo pathfinding fisico.
        Vector3 posicaoAtual = npcAtual.transform.position;
        Vector3 destinoPlano = new Vector3(destino.x, posicaoAtual.y, destino.z);
        Vector3 direcao = destinoPlano - posicaoAtual;
        direcao.y = 0f;

        if (direcao.sqrMagnitude <= distanciaChegada * distanciaChegada)
        {
            npcAtual.transform.position = destinoPlano;
            return true;
        }

        npcAtual.transform.position = Vector3.MoveTowards(posicaoAtual, destinoPlano, velocidadeNpc * Time.deltaTime);

        if (direcao.sqrMagnitude > 0.0001f)
        {
            // Mantem a rotacao apenas no plano horizontal para evitar movimento torto.
            npcAtual.transform.rotation = Quaternion.LookRotation(direcao.normalized, Vector3.up);
        }

        return false;
    }

    bool MoverNpcComPathfindingFisico(Vector3 destino, float distanciaChegada)
    {
        // Metodo principal de movimento do cliente: recebe um destino e segue um caminho aprovado por colisao.
        if (npcAtual == null)
        {
            return false;
        }

        // Mantem o destino no mesmo Y do NPC para evitar que altura de prateleira puxe o personagem para cima/baixo.
        Vector3 posicaoAtual = npcAtual.transform.position;
        Vector3 destinoPlano = new Vector3(destino.x, posicaoAtual.y, destino.z);

        // Se ja chegou perto o suficiente, encerra o caminho atual.
        if (Vector3.Distance(posicaoAtual, destinoPlano) <= distanciaChegada)
        {
            npcAtual.transform.position = destinoPlano;
            return true;
        }

        // O desvio fisico testa poucos passos por frame, evitando travar o jogo com busca em grade.
        TentarAndarComDesvio(destinoPlano);
        return false;
    }

    void TentarAndarComDesvio(Vector3 destino)
    {
        Vector3 posicaoAtual = npcAtual.transform.position;
        Vector3 direcaoPrincipal = destino - posicaoAtual;
        direcaoPrincipal.y = 0f;

        if (direcaoPrincipal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direcaoPrincipal.Normalize();

        // Primeiro tenta andar reto; se tiver parede, tenta pequenas aberturas laterais.
        float[] angulos = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
        for (int i = 0; i < angulos.Length; i++)
        {
            Vector3 direcaoTeste = Quaternion.Euler(0f, angulos[i], 0f) * direcaoPrincipal;
            if (TentarMoverPassoFisico(direcaoTeste))
            {
                return;
            }
        }
    }

    bool TentarMoverPassoFisico(Vector3 direcao)
    {
        // Move apenas um passo pequeno ja validado contra os colliders reais da loja.
        Vector3 posicaoAtual = npcAtual.transform.position;
        Vector3 proximaPosicao = posicaoAtual + direcao.normalized * velocidadeNpc * Time.deltaTime;

        if (!PosicaoLivreParaNpc(proximaPosicao))
        {
            return false;
        }

        if (TemParedeEntrePontos(posicaoAtual, proximaPosicao))
        {
            return false;
        }

        npcAtual.transform.position = proximaPosicao;
        npcAtual.transform.rotation = Quaternion.LookRotation(direcao.normalized, Vector3.up);
        return true;
    }

    bool PosicaoLivreParaNpc(Vector3 posicao)
    {
        // Primeiro confirma que existe piso abaixo desse ponto.
        if (!TemChaoAbaixo(posicao))
        {
            return false;
        }

        // Depois checa volumes horizontais do corpo; isso evita confundir o chao com parede.
        Vector3 pontoBaixo = posicao + Vector3.up * 0.45f;
        Vector3 pontoAlto = posicao + Vector3.up * Mathf.Max(0.9f, alturaAgenteNpc * 0.75f);

        if (ExisteParedeNoPonto(pontoBaixo) || ExisteParedeNoPonto(pontoAlto))
        {
            return false;
        }

        return true;
    }

    bool ExisteParedeNoPonto(Vector3 pontoCorpo)
    {
        // Usa uma esfera na altura do corpo para detectar paredes/prateleiras sem encostar no chao.
        Collider[] colisoes = Physics.OverlapSphere(pontoCorpo, raioAgenteNpc, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < colisoes.Length; i++)
        {
            Collider colisao = colisoes[i];
            if (!ColliderBloqueiaNpc(colisao))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    bool TemParedeEntrePontos(Vector3 origem, Vector3 destino)
    {
        // CapsuleCast bloqueia o passo quando a "capsula do corpo" bateria em parede do modelo da loja.
        Vector3 deslocamento = destino - origem;
        deslocamento.y = 0f;
        float distancia = deslocamento.magnitude;

        if (distancia <= 0.0001f)
        {
            return false;
        }

        Vector3 direcao = deslocamento / distancia;
        Vector3 baseCapsula = origem + Vector3.up * 0.35f;
        Vector3 topoCapsula = origem + Vector3.up * Mathf.Max(0.7f, alturaAgenteNpc - 0.15f);
        RaycastHit[] impactos = Physics.CapsuleCastAll(baseCapsula, topoCapsula, raioAgenteNpc, direcao, distancia, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < impactos.Length; i++)
        {
            RaycastHit impacto = impactos[i];
            if (!ColliderBloqueiaNpc(impacto.collider))
            {
                continue;
            }

            if (impacto.normal.y > 0.55f)
            {
                // Normal apontando para cima e piso/rampa, nao parede.
                continue;
            }

            return true;
        }

        return false;
    }

    bool ColliderBloqueiaNpc(Collider colisao)
    {
        if (colisao == null)
        {
            return false;
        }

        if (npcAtual != null && colisao.transform.IsChildOf(npcAtual.transform))
        {
            return false;
        }

        if (colisao.isTrigger)
        {
            return false;
        }

        if (colisao.GetComponentInParent<Pickup>() != null)
        {
            // Produtos pequenos nao bloqueiam rota de cliente.
            return false;
        }

        return true;
    }

    bool TemChaoAbaixo(Vector3 posicao)
    {
        // Raycast vertical: se algo solido esta logo abaixo do pe, a celula e pisavel.
        RaycastHit[] impactos = Physics.RaycastAll(posicao + Vector3.up * 2f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < impactos.Length; i++)
        {
            Collider colisao = impactos[i].collider;
            if (colisao == null)
            {
                continue;
            }

            if (npcAtual != null && colisao.transform.IsChildOf(npcAtual.transform))
            {
                continue;
            }

            if (colisao.GetComponentInParent<Pickup>() != null)
            {
                continue;
            }

            // Considera chao apenas quando a superficie esta abaixo do pe do NPC.
            if (impactos[i].point.y <= posicao.y + 0.25f)
            {
                return true;
            }
        }

        return false;
    }

    bool SegmentoLivreParaNpc(Vector3 origem, Vector3 destino)
    {
        // Mantido como leitura simples para outros trechos: livre significa sem parede no caminho.
        return !TemParedeEntrePontos(origem, destino) && PosicaoLivreParaNpc(destino);
    }

    void PrepararPontosDaCena()
    {
        if (spawnNPC == null)
        {
            spawnNPC = ObterPrimeiroTransformValido(caminhoEntrada);
        }

        if (despawnNPC == null)
        {
            despawnNPC = ObterUltimoTransformValido(caminhoSaida);
        }

        if (spawnProduto == null)
        {
            spawnProduto = ObterPrimeiroTransformValido(pontosSpawn);
        }

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
    }

    void PrepararColisaoDaLoja()
    {
        AutoShopCollision colisaoLoja = Object.FindFirstObjectByType<AutoShopCollision>();
        if (colisaoLoja == null)
        {
            GameObject alvo = storeModel != null ? storeModel.gameObject : GameObject.Find("StoreModel");
            if (alvo == null)
            {
                alvo = GameObject.Find("shop");
            }

            if (alvo == null)
            {
                Debug.LogWarning("SistemaCaixa nao encontrou StoreModel/shop para preparar colisoes da loja.", this);
                return;
            }

            // Adiciona o preparador automaticamente para nao depender de configuracao manual.
            colisaoLoja = alvo.AddComponent<AutoShopCollision>();
        }

        colisaoLoja.PrepararColisoesDaLoja();
    }

    bool TentarCalcularPontoAproximacaoProduto(Transform produto, out Vector3 pontoAproximacao)
    {
        pontoAproximacao = Vector3.zero;
        if (produto == null)
        {
            return false;
        }

        // Calcula um ponto ao redor do produto, mas so aceita se houver caminho fisico ate ele.
        Bounds bounds = ObterBoundsProduto(produto);
        Vector3 centro = bounds.center;
        Vector3 origem = npcAtual != null ? npcAtual.transform.position : spawnNPC.position;
        Vector3 direcaoBase = centro - origem;
        direcaoBase.y = 0f;

        if (direcaoBase.sqrMagnitude < 0.01f)
        {
            direcaoBase = Vector3.forward;
        }

        direcaoBase.Normalize();

        float raioProduto = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float distancia = raioProduto + distanciaAproximacaoProduto;
        Vector3 melhorPonto = centro - direcaoBase * distancia;
        melhorPonto.y = npcAtual != null ? npcAtual.transform.position.y : spawnNPC.position.y;

        if (TentarObterPontoComCaminho(melhorPonto, out pontoAproximacao))
        {
            return true;
        }

        // Testa pontos em volta do produto para achar um corredor navegavel perto da prateleira.
        for (int i = 0; i < 16; i++)
        {
            float angulo = (360f / 16f) * i;
            Vector3 direcao = Quaternion.Euler(0f, angulo, 0f) * Vector3.forward;
            Vector3 candidato = centro + direcao * distancia;
            candidato.y = npcAtual != null ? npcAtual.transform.position.y : spawnNPC.position.y;

            if (TentarObterPontoComCaminho(candidato, out pontoAproximacao))
            {
                return true;
            }
        }

        return false;
    }

    Bounds ObterBoundsProduto(Transform produto)
    {
        Renderer[] renderers = produto.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(produto.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    bool TentarObterPontoComCaminho(Vector3 candidato, out Vector3 pontoNavegavel)
    {
        pontoNavegavel = candidato;
        if (npcAtual == null)
        {
            return PosicaoLivreParaNpc(candidato);
        }

        // No sistema leve, o ponto precisa estar livre; o desvio por frame cuida do caminho ate ele.
        return PosicaoLivreParaNpc(candidato);
    }

    void PrepararInteractables()
    {
        if (interactables != null)
        {
            return;
        }

        // Procura a aba/objeto Interactables automaticamente para pegar produtos da prateleira.
        GameObject interactablesObject = GameObject.Find("Interactables");
        if (interactablesObject != null)
        {
            interactables = interactablesObject.transform;
        }
    }

    void PrepararStoreModel()
    {
        if (storeModel != null)
        {
            return;
        }

        // Procura o StoreModel automaticamente para achar os itens de estoque.
        GameObject storeModelObject = GameObject.Find("StoreModel");
        if (storeModelObject != null)
        {
            storeModel = storeModelObject.transform;
        }
    }

    void PrepararProdutosDaPrateleira()
    {
        if (interactables == null)
        {
            return;
        }

        Pickup[] pickups = interactables.GetComponentsInChildren<Pickup>(true);
        for (int i = 0; i < pickups.Length; i++)
        {
            Pickup pickup = pickups[i];
            Transform raizProduto = ObterRaizProdutoDentroDeInteractables(pickup);
            pickup.ConfigurarComoProdutoDePrateleira(raizProduto);
        }
    }

    void PrepararProdutosDoEstoque()
    {
        if (storeModel == null)
        {
            return;
        }

        Pickup[] pickups = storeModel.GetComponentsInChildren<Pickup>(true);
        for (int i = 0; i < pickups.Length; i++)
        {
            Pickup pickup = pickups[i];
            if (ProdutoPertenceAInteractables(pickup))
            {
                continue;
            }

            Transform raizProduto = ObterRaizProdutoDentroDoEstoque(pickup);
            pickup.ConfigurarComoProdutoDeEstoque(raizProduto);
        }
    }

    void PrepararConfiguracoesClientes()
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

            if (configuracao.npc != null && configuracao.npc.scene.IsValid())
            {
                NPCInteraction interacao = configuracao.npc.GetComponent<NPCInteraction>();
                if (interacao != null)
                {
                    // Desliga a interacao manual no modelo-base do NPC.
                    interacao.enabled = false;
                }

                // Mantem o modelo-base do NPC escondido quando ele for um objeto da cena.
                configuracao.npc.SetActive(false);
            }

            if (configuracao.produto != null && configuracao.produto.gameObject.scene.IsValid())
            {
                // Mantem o produto-base escondido quando ele for um objeto da cena.
                configuracao.produto.gameObject.SetActive(false);
            }
        }
    }

    void PrepararIndicesConfiguracoesValidas()
    {
        indicesConfiguracoesValidas.Clear();

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

            if (configuracao.npc == null || configuracao.produto == null)
            {
                continue;
            }

            // Guarda apenas as configuracoes que tem NPC e produto prontos.
            indicesConfiguracoesValidas.Add(i);
        }
    }

    bool TentarEscolherConfiguracaoCliente()
    {
        PrepararIndicesConfiguracoesValidas();
        configuracaoAtual = null;

        if (indicesConfiguracoesValidas.Count == 0)
        {
            return false;
        }

        int indiceListaEscolhido = Random.Range(0, indicesConfiguracoesValidas.Count);

        if (evitarRepetirNpcEmSequencia && indicesConfiguracoesValidas.Count > 1)
        {
            // Tenta evitar repetir a mesma variacao duas vezes seguidas.
            int tentativas = 0;
            while (indicesConfiguracoesValidas[indiceListaEscolhido] == ultimoIndiceConfiguracao && tentativas < 10)
            {
                indiceListaEscolhido = Random.Range(0, indicesConfiguracoesValidas.Count);
                tentativas++;
            }
        }

        ultimoIndiceConfiguracao = indicesConfiguracoesValidas[indiceListaEscolhido];
        configuracaoAtual = configuracoesClientes[ultimoIndiceConfiguracao];
        return configuracaoAtual != null;
    }

    void PrepararCaminhoOrdenado(Transform[] caminhoBase, List<Transform> caminhoAtual)
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
                // Mantem exatamente a ordem colocada no Inspector para este NPC.
                caminhoAtual.Add(caminhoBase[i]);
            }
        }
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
                // Copia somente os pontos validos para a rota deste cliente.
                caminhoAtual.Add(caminhoBase[i]);
            }
        }

        if (sortearPontos)
        {
            // Embaralha os pontos para o modo antigo ter variacao.
            EmbaralharCaminho(caminhoAtual);
        }
    }

    void EmbaralharCaminho(List<Transform> caminho)
    {
        for (int i = caminho.Count - 1; i > 0; i--)
        {
            // Fisher-Yates: troca cada ponto com outro indice aleatorio da lista.
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
                // Evita repetir o ponto usado apenas como spawn ou despawn.
                caminho.RemoveAt(i);
            }
        }
    }

    void PrepararModelosNpc()
    {
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

        // Ordena para manter o pareamento previsivel entre NPC1, NPC2 e os produtos.
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

                if (ProdutoPertenceAInteractables(pickupDaCena))
                {
                    // Produtos da prateleira sao escolhidos pelo fluxo novo, nao viram modelo escondido.
                    continue;
                }

                AdicionarModeloProduto(pickupDaCena);
            }
        }

        // Ordena para manter o pareamento previsivel entre Box1, Box2 e os NPCs.
        modelosProduto.Sort((a, b) => string.Compare(a.name, b.name));
    }

    void AdicionarModeloNpc(GameObject modelo)
    {
        if (modelo == null || modelosNpc.Contains(modelo))
        {
            return;
        }

        NPCInteraction interacao = modelo.GetComponent<NPCInteraction>();
        if (interacao != null)
        {
            // Desliga a interacao manual no modelo para evitar conflito com o cliente do caixa.
            interacao.enabled = false;
        }

        if (modelo.scene.IsValid())
        {
            // Mantem o modelo-base escondido quando ele for um objeto da cena.
            modelo.SetActive(false);
        }

        modelosNpc.Add(modelo);
    }

    void AdicionarModeloProduto(Pickup modelo)
    {
        if (modelo == null || modelosProduto.Contains(modelo))
        {
            return;
        }

        if (ProdutoPertenceAInteractables(modelo))
        {
            // Mantem produtos reais da prateleira visiveis no cenario.
            return;
        }

        Transform raizModelo = modelo.ObterRaizProduto();
        if (raizModelo != null && raizModelo.gameObject.scene.IsValid())
        {
            // Mantem o produto-base escondido quando ele for um objeto da cena.
            raizModelo.gameObject.SetActive(false);
        }

        modelosProduto.Add(modelo);
    }

    Pickup ObterProdutoDoNpc(int indiceNpc)
    {
        if (modelosProduto.Count == 0)
        {
            return null;
        }

        // Faz o pareamento por ordem para que NPC1 puxe Box1, NPC2 puxe Box2 e assim por diante.
        int indiceProduto = indiceNpc % modelosProduto.Count;
        return modelosProduto[indiceProduto];
    }

    Pickup SpawnarProdutoAtual()
    {
        if (modeloProdutoAtual == null || spawnProduto == null)
        {
            return null;
        }

        if (pickupProdutoCaixaPendente != null)
        {
            // Mostra no caixa a copia que foi criada antes da prateleira ser esvaziada.
            return ColocarProdutoPendenteNoSpawn();
        }

        if (produtoDaPrateleiraAtual != null)
        {
            // Vende uma copia do produto; o produto-base da prateleira fica escondido como slot vazio.
            return CriarCopiaDoProdutoNoCaixa(produtoDaPrateleiraAtual);
        }

        Transform raizModelo = modeloProdutoAtual.ObterRaizProduto();
        if (raizModelo == null)
        {
            return null;
        }

        return CriarCopiaDoProdutoNoCaixa(modeloProdutoAtual);
    }

    void PrepararProdutoPendenteDoCaixa(Pickup produtoBase)
    {
        LimparProdutoPendenteDoCaixa();

        Pickup copia = CriarCopiaDoProdutoNoCaixa(produtoBase);
        if (copia == null)
        {
            return;
        }

        Transform raizCopia = copia.ObterRaizProduto();
        produtoCaixaPendente = raizCopia != null ? raizCopia.gameObject : copia.gameObject;
        pickupProdutoCaixaPendente = copia;

        // A copia ja existe antes do item da prateleira sumir, mas so aparece quando o cliente chega ao caixa.
        produtoCaixaPendente.SetActive(false);
    }

    Pickup ColocarProdutoPendenteNoSpawn()
    {
        if (pickupProdutoCaixaPendente == null || produtoCaixaPendente == null || spawnProduto == null)
        {
            LimparProdutoPendenteDoCaixa();
            return null;
        }

        produtoCaixaPendente.transform.SetParent(null);
        produtoCaixaPendente.transform.position = spawnProduto.position;
        produtoCaixaPendente.transform.rotation = spawnProduto.rotation;
        AtivarObjetoInteiro(produtoCaixaPendente.transform);
        pickupProdutoCaixaPendente.ConfigurarComoProdutoDoCaixa(produtoCaixaPendente.transform);

        Pickup produtoPronto = pickupProdutoCaixaPendente;
        produtoCaixaPendente = null;
        pickupProdutoCaixaPendente = null;
        return produtoPronto;
    }

    void LimparProdutoPendenteDoCaixa()
    {
        if (produtoCaixaPendente != null)
        {
            Destroy(produtoCaixaPendente);
        }

        produtoCaixaPendente = null;
        pickupProdutoCaixaPendente = null;
    }

    Pickup CriarCopiaDoProdutoNoCaixa(Pickup produtoBase)
    {
        Transform raizModelo = produtoBase.ObterRaizProduto();
        if (raizModelo == null)
        {
            return null;
        }

        // Clona a raiz completa do produto para assets formados por varios children.
        GameObject novoObjeto = Instantiate(raizModelo.gameObject, spawnProduto.position, spawnProduto.rotation);
        novoObjeto.name = raizModelo.name;
        novoObjeto.SetActive(true);
        AtivarObjetoInteiro(novoObjeto.transform);

        Pickup novoProduto = novoObjeto.GetComponentInChildren<Pickup>(true);
        if (novoProduto == null)
        {
            Debug.LogWarning("O produto clonado nao possui Pickup em nenhum child.", novoObjeto);
            return null;
        }

        // Marca a copia como item vendido para ela sumir depois de passar no scanner.
        novoProduto.ConfigurarComoProdutoDoCaixa(novoObjeto.transform);
        return novoProduto;
    }

    void AtivarObjetoInteiro(Transform raiz)
    {
        if (raiz == null)
        {
            return;
        }

        // Garante que a copia apareca mesmo quando o produto-base estava escondido na prateleira.
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

    bool TentarEscolherProdutoDaPrateleira()
    {
        if (interactables == null)
        {
            return false;
        }

        PrepararProdutosDaPrateleira();

        List<Pickup> produtosDisponiveis = new List<Pickup>();
        Pickup[] pickups = interactables.GetComponentsInChildren<Pickup>(true);
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i].EstaDisponivelNaPrateleira())
            {
                produtosDisponiveis.Add(pickups[i]);
            }
        }

        if (produtosDisponiveis.Count == 0)
        {
            return false;
        }

        produtoDaPrateleiraAtual = produtosDisponiveis[Random.Range(0, produtosDisponiveis.Count)];
        if (!produtoDaPrateleiraAtual.ReservarParaNpc())
        {
            produtoDaPrateleiraAtual = null;
            return false;
        }

        raizProdutoDaPrateleiraAtual = produtoDaPrateleiraAtual.ObterRaizProduto();
        return raizProdutoDaPrateleiraAtual != null;
    }

    Transform ObterRaizProdutoDentroDeInteractables(Pickup pickup)
    {
        if (pickup == null || interactables == null)
        {
            return pickup != null ? pickup.transform : null;
        }

        Transform atual = pickup.transform;
        while (atual.parent != null && atual.parent != interactables)
        {
            atual = atual.parent;
        }

        // O filho direto de Interactables representa o produto completo na prateleira.
        return atual;
    }

    Transform ObterRaizProdutoDentroDoEstoque(Pickup pickup)
    {
        if (pickup == null)
        {
            return null;
        }

        Transform containerEstoque = ObterContainerEstoque(pickup.transform);
        if (containerEstoque == null)
        {
            containerEstoque = storeModel;
        }

        Transform atual = pickup.transform;
        while (atual.parent != null && atual.parent != containerEstoque)
        {
            atual = atual.parent;
        }

        // O filho direto do container de estoque representa o produto completo.
        return atual;
    }

    Transform ObterContainerEstoque(Transform origem)
    {
        Transform atual = origem;
        while (atual != null && atual != storeModel)
        {
            string nome = atual.name.ToLowerInvariant();
            if (nome.Contains("estoque") || nome.Contains("stock"))
            {
                return atual;
            }

            atual = atual.parent;
        }

        return null;
    }

    bool ProdutoPertenceAInteractables(Pickup pickup)
    {
        if (pickup == null || interactables == null)
        {
            return false;
        }

        return pickup.transform == interactables || pickup.transform.IsChildOf(interactables);
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

        // Tenta evitar repetir o mesmo NPC duas vezes seguidas.
        int tentativas = 0;
        while (indiceSorteado == ultimoIndiceNpc && tentativas < 10)
        {
            indiceSorteado = Random.Range(0, modelosNpc.Count);
            tentativas++;
        }

        return indiceSorteado;
    }

    void ProcessarDigitacaoTroco()
    {
        bool houveMudanca = false;

        // Le os caracteres digitados neste frame para montar o valor do troco.
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

        // Monta a mensagem completa exibida na tela enquanto o jogador digita.
        string textoTroco =
            mensagemTrocoAtual +
            "\nTroco digitado: R$ " + FormatarTrocoDigitado();

        if (!string.IsNullOrWhiteSpace(avisoTrocoAtual))
        {
            textoTroco += "\n" + avisoTrocoAtual;
        }

        scanner.MostrarTextoInfoProduto(textoTroco, 0f);
    }

    Vector3 CalcularPontoParadaNoCaixa()
    {
        if (scanner == null || spawnNPC == null || despawnNPC == null)
        {
            return Vector3.zero;
        }

        Vector3 pontoParada = scanner.transform.position;
        Vector3 eixoEntradaSaida = despawnNPC.position - spawnNPC.position;
        eixoEntradaSaida.y = 0f;

        // Mantem o NPC alinhado ao mesmo corredor principal da entrada e da saida.
        if (Mathf.Abs(eixoEntradaSaida.x) >= Mathf.Abs(eixoEntradaSaida.z))
        {
            pontoParada.z = spawnNPC.position.z;
            pontoParada.y = spawnNPC.position.y;
        }
        else
        {
            pontoParada.x = spawnNPC.position.x;
            pontoParada.y = spawnNPC.position.y;
        }

        return pontoParada;
    }

    Transform ObterPrimeiroTransformValido(Transform[] pontos)
    {
        if (pontos == null || pontos.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < pontos.Length; i++)
        {
            if (pontos[i] != null)
            {
                return pontos[i];
            }
        }

        return null;
    }

    Transform ObterUltimoTransformValido(Transform[] pontos)
    {
        if (pontos == null || pontos.Length == 0)
        {
            return null;
        }

        for (int i = pontos.Length - 1; i >= 0; i--)
        {
            if (pontos[i] != null)
            {
                return pontos[i];
            }
        }

        return null;
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
            // Se nenhum valor configurado cobrir a compra, cria um fallback simples.
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

    void LimparAtendimentoAtual()
    {
        if (npcAtual != null)
        {
            Destroy(npcAtual);
            npcAtual = null;
        }

        if (produtoAtual != null)
        {
            // Remove a raiz inteira do produto clonado, nao apenas o child que tem o Pickup.
            Transform raizProdutoAtual = produtoAtual.ObterRaizProduto();
            Destroy(raizProdutoAtual != null ? raizProdutoAtual.gameObject : produtoAtual.gameObject);
            produtoAtual = null;
        }

        LimparProdutoPendenteDoCaixa();
        trocoDigitadoEmCentavos = string.Empty;
        mensagemTrocoAtual = string.Empty;
        avisoTrocoAtual = string.Empty;
        configuracaoAtual = null;
        produtoDaPrateleiraAtual = null;
        raizProdutoDaPrateleiraAtual = null;
        caminhoEntradaAtual.Clear();
        caminhoSaidaAtual.Clear();
        indiceWaypointEntradaAtual = 0;
        indiceWaypointSaidaAtual = 0;
    }
}
