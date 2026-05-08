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

    // Ajustes de comportamento.
    [SerializeField] float velocidadeNpc = 2.2f;
    [SerializeField] float tempoEntreClientes = 4f;
    [SerializeField] float distanciaEntregaTroco = 2.5f;
    [SerializeField] float distanciaChegadaWaypoint = 0.08f;
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

        if (indicesConfiguracoesValidas.Count == 0 && (modelosNpc.Count == 0 || modelosProduto.Count == 0))
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

        if (estadoAtual == EstadoCliente.IndoAoCaixa)
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
        Vector3 posicaoAtual = npcAtual.transform.position;
        Vector3 destinoPlano = new Vector3(destino.x, posicaoAtual.y, destino.z);
        Vector3 direcao = destinoPlano - posicaoAtual;
        direcao.y = 0f;

        if (direcao.sqrMagnitude <= distanciaChegadaWaypoint * distanciaChegadaWaypoint)
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

        if (modelo.gameObject.scene.IsValid())
        {
            // Mantem o produto-base escondido quando ele for um objeto da cena.
            modelo.gameObject.SetActive(false);
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

        // Cria uma copia do produto no ponto de spawn configurado.
        Pickup novoProduto = Instantiate(modeloProdutoAtual, spawnProduto.position, spawnProduto.rotation);
        novoProduto.name = modeloProdutoAtual.name;
        novoProduto.gameObject.SetActive(true);
        return novoProduto;
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
            Destroy(produtoAtual.gameObject);
            produtoAtual = null;
        }

        trocoDigitadoEmCentavos = string.Empty;
        mensagemTrocoAtual = string.Empty;
        avisoTrocoAtual = string.Empty;
        configuracaoAtual = null;
        caminhoEntradaAtual.Clear();
        caminhoSaidaAtual.Clear();
        indiceWaypointEntradaAtual = 0;
        indiceWaypointSaidaAtual = 0;
    }
}
