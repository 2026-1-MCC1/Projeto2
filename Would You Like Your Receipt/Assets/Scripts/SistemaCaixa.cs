using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    readonly List<GameObject> modelosNpc = new List<GameObject>();
    readonly List<Pickup> modelosProduto = new List<Pickup>();
    readonly List<Transform> caminhoEntradaAtual = new List<Transform>();
    readonly List<Transform> caminhoSaidaAtual = new List<Transform>();

    EstadoCliente estadoAtual = EstadoCliente.AguardandoProximoCliente;

    GameObject npcAtual;
    Pickup produtoAtual;
    Pickup modeloProdutoAtual;

    int ultimoIndiceNpc = -1;
    int indiceWaypointEntradaAtual;
    int indiceWaypointSaidaAtual;

    float totalCompra;
    float valorPago;
    float troco;

    bool sistemaPronto;

    string trocoDigitadoEmCentavos = string.Empty;
    string mensagemTrocoAtual = string.Empty;
    string avisoTrocoAtual = string.Empty;

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

        // Monta as listas de NPCs e produtos a partir do Inspector e do que ja existe na cena.
        PrepararModelosNpc();
        PrepararModelosProduto();

        if (scanner == null || spawnNPC == null || despawnNPC == null || spawnProduto == null)
        {
            Debug.LogWarning("SistemaCaixa nao encontrou Scanner, SpawnNPC, DespawnNPC ou SpawnObjects.", this);
            enabled = false;
            return;
        }

        if (modelosNpc.Count == 0 || modelosProduto.Count == 0)
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
            // Faz o NPC seguir pelos Empty Objects da entrada antes de parar no caixa.
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
            // Faz o NPC seguir pelos Empty Objects da saida antes de sumir da cena.
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
        LimparAtendimentoAtual();

        int indiceNpcEscolhido = SortearIndiceNpc();
        GameObject modeloNpcEscolhido = modelosNpc[indiceNpcEscolhido];
        modeloProdutoAtual = ObterProdutoDoNpc(indiceNpcEscolhido);

        if (modeloNpcEscolhido == null || modeloProdutoAtual == null)
        {
            Debug.LogWarning("SistemaCaixa nao conseguiu escolher um NPC ou produto valido.", this);
            estadoAtual = EstadoCliente.AguardandoProximoCliente;
            return;
        }

        totalCompra = 0f;
        valorPago = 0f;
        troco = 0f;

        npcAtual = Instantiate(modeloNpcEscolhido, spawnNPC.position, spawnNPC.rotation);
        npcAtual.name = modeloNpcEscolhido.name;
        npcAtual.SetActive(true);

        // Sorteia os waypoints deste atendimento para cada cliente fazer um caminho diferente.
        PrepararCaminhoAtual(caminhoEntrada, caminhoEntradaAtual, sortearCaminhoEntrada);
        PrepararCaminhoAtual(caminhoSaida, caminhoSaidaAtual, sortearCaminhoSaida);
        RemoverPontoDoCaminho(caminhoEntradaAtual, spawnNPC);
        RemoverPontoDoCaminho(caminhoSaidaAtual, despawnNPC);
        indiceWaypointEntradaAtual = 0;
        indiceWaypointSaidaAtual = 0;

        NPCInteraction interacaoClone = npcAtual.GetComponent<NPCInteraction>();
        if (interacaoClone != null)
        {
            // O cliente do caixa nao precisa da interacao manual enquanto estiver no fluxo.
            interacaoClone.enabled = false;
        }

        estadoAtual = EstadoCliente.IndoAoCaixa;
        ultimoIndiceNpc = indiceNpcEscolhido;
    }

    void AoChegarNoCaixa()
    {
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

        // Ignora referencias vazias para nao travar o NPC se algum Empty Object for removido.
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
            // Avanca para o proximo Empty Object sorteado antes de ir para o destino final.
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
            // Mantem a rotacao apenas no plano horizontal para evitar giro torto.
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
                // Copia somente os Empty Objects validos para a rota deste cliente.
                caminhoAtual.Add(caminhoBase[i]);
            }
        }

        if (sortearPontos)
        {
            // Embaralha os pontos para cada NPC ter uma variacao de caminho.
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
                // Evita sortear o ponto usado apenas como spawn ou despawn.
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

        modelo.SetActive(false);
        modelosNpc.Add(modelo);
    }

    void AdicionarModeloProduto(Pickup modelo)
    {
        if (modelo == null || modelosProduto.Contains(modelo))
        {
            return;
        }

        modelo.gameObject.SetActive(false);
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

        // Se o fluxo principal for horizontal, mantem o NPC andando da esquerda para a direita.
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
            return preco + 10f;
        }

        return valoresValidos[Random.Range(0, valoresValidos.Count)];
    }

    string FormatarDinheiro(float valor)
    {
        return valor.ToString("F2").Replace(".", ",");
    }

    float ObterValorTrocoDigitado()
    {
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
    }
}
