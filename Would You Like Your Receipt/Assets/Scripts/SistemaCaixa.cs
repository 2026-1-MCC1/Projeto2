using System.Collections;
using UnityEngine;

public class SistemaCaixa : MonoBehaviour
{
    enum EstadoCliente
    {
        AguardandoProximoCliente,
        IndoAoCaixa,
        AguardandoEscaneamento,
        AguardandoTroco,
        IndoEmbora
    }

    [SerializeField] Scanner scanner;
    [SerializeField] GoalManager goalManager;
    [SerializeField] Transform jogador;
    [SerializeField] GameObject npcCliente;
    [SerializeField] Pickup modeloProduto;

    [SerializeField] float velocidadeNpc = 2.2f;
    [SerializeField] float tempoEntreClientes = 4f;
    [SerializeField] float distanciaParadaNoScanner = 1.7f;
    [SerializeField] float distanciaEntregaTroco = 2.5f;
    [SerializeField] float distanciaSpawnProduto = 1.25f;
    [SerializeField] float alturaSpawnProduto = 0.8f;
    [SerializeField] KeyCode teclaEntregarTroco = KeyCode.T;
    [SerializeField] float[] valoresPagamento = { 20f, 50f, 100f };

    EstadoCliente estadoAtual = EstadoCliente.AguardandoProximoCliente;

    Vector3 posicaoSpawnNpc;
    Vector3 posicaoSaidaNpc;
    Vector3 posicaoParadaNpc;

    Pickup produtoAtual;
    NPCInteraction interacaoNpc;

    float valorPagoAtual;
    float trocoAtual;

    bool sistemaPronto;

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

        if (npcCliente == null)
        {
            GameObject npcEncontrado = GameObject.Find("NPC1");
            if (npcEncontrado != null)
            {
                npcCliente = npcEncontrado;
            }
        }

        if (modeloProduto == null)
        {
            GameObject produtoEncontrado = GameObject.Find("Box1");
            if (produtoEncontrado != null)
            {
                modeloProduto = produtoEncontrado.GetComponent<Pickup>();
            }
        }

        if (scanner == null || npcCliente == null || modeloProduto == null)
        {
            Debug.LogWarning("SistemaCaixa nao encontrou Scanner, NPC1 ou Box1 na cena.", this);
            enabled = false;
            return;
        }

        interacaoNpc = npcCliente.GetComponent<NPCInteraction>();
        if (interacaoNpc != null)
        {
            // Desliga a interacao manual antiga para o NPC do caixa usar apenas este sistema.
            interacaoNpc.enabled = false;
        }

        posicaoSpawnNpc = npcCliente.transform.position;
        posicaoParadaNpc = CalcularPosicaoParadaNpc();
        posicaoSaidaNpc = posicaoSpawnNpc + ObterDirecaoCliente() * 3f;

        // Esconde o cliente e o produto-base ate o momento de uso.
        npcCliente.SetActive(false);
        modeloProduto.gameObject.SetActive(false);
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
            MoverNpc(posicaoParadaNpc, EstadoCliente.AguardandoEscaneamento, AoChegarNoCaixa);
        }
        else if (estadoAtual == EstadoCliente.IndoEmbora)
        {
            MoverNpc(posicaoSaidaNpc, EstadoCliente.AguardandoProximoCliente, AoClienteIrEmbora);
        }
        else if (estadoAtual == EstadoCliente.AguardandoTroco)
        {
            // Permite concluir a compra quando o jogador estiver perto do caixa.
            if (Input.GetKeyDown(teclaEntregarTroco) && JogadorPertoDoScanner())
            {
                EntregarTroco();
            }
        }
    }

    private IEnumerator RotinaClientes()
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

    private void IniciarAtendimento()
    {
        valorPagoAtual = 0f;
        trocoAtual = 0f;
        produtoAtual = null;

        npcCliente.transform.position = posicaoSpawnNpc;
        npcCliente.SetActive(true);
        estadoAtual = EstadoCliente.IndoAoCaixa;
    }

    private void AoChegarNoCaixa()
    {
        produtoAtual = SpawnarProdutoDoCliente();

        if (produtoAtual != null)
        {
            scanner.MostrarTextoInfoProduto(
                "Cliente no caixa.\n\n" +
                produtoAtual.ObterMensagemEscaneamento() +
                "\n\nEscaneie o produto para receber o pagamento.",
                0f
            );
        }
    }

    private void AoProdutoEscaneado(Pickup pickup)
    {
        if (estadoAtual != EstadoCliente.AguardandoEscaneamento || pickup != produtoAtual)
        {
            return;
        }

        valorPagoAtual = SortearValorPago(pickup.ObterPrecoProduto());
        trocoAtual = Mathf.Max(0f, valorPagoAtual - pickup.ObterPrecoProduto());
        estadoAtual = EstadoCliente.AguardandoTroco;

        scanner.MostrarTextoInfoProduto(
            pickup.ObterMensagemEscaneamento() +
            "\n\nCliente pagou: R$ " + FormatarDinheiro(valorPagoAtual) +
            "\nTroco devido: R$ " + FormatarDinheiro(trocoAtual) +
            "\nAperte " + teclaEntregarTroco + " perto do caixa para entregar o troco.",
            0f
        );
    }

    private void EntregarTroco()
    {
        if (goalManager != null)
        {
            goalManager.ClienteAtendido();
            goalManager.MostrarMensagem("Troco entregue: R$ " + FormatarDinheiro(trocoAtual), 3f);
        }

        scanner.MostrarTextoInfoProduto(
            "Troco entregue.\nCliente atendido com sucesso.",
            2.5f
        );

        estadoAtual = EstadoCliente.IndoEmbora;
    }

    private void AoClienteIrEmbora()
    {
        npcCliente.SetActive(false);
        estadoAtual = EstadoCliente.AguardandoProximoCliente;
    }

    private void MoverNpc(Vector3 destino, EstadoCliente proximoEstado, System.Action aoChegar)
    {
        Vector3 posicaoAtual = npcCliente.transform.position;
        Vector3 destinoPlano = new Vector3(destino.x, posicaoAtual.y, destino.z);
        Vector3 direcao = destinoPlano - posicaoAtual;
        direcao.y = 0f;

        if (direcao.sqrMagnitude <= 0.01f)
        {
            npcCliente.transform.position = destinoPlano;
            estadoAtual = proximoEstado;
            aoChegar?.Invoke();
            return;
        }

        npcCliente.transform.position = Vector3.MoveTowards(posicaoAtual, destinoPlano, velocidadeNpc * Time.deltaTime);

        if (direcao.sqrMagnitude > 0.0001f)
        {
            npcCliente.transform.rotation = Quaternion.LookRotation(direcao.normalized, Vector3.up);
        }
    }

    private Pickup SpawnarProdutoDoCliente()
    {
        Pickup novoProduto = Instantiate(modeloProduto, CalcularPosicaoSpawnProduto(), modeloProduto.transform.rotation);
        novoProduto.gameObject.name = modeloProduto.gameObject.name;
        novoProduto.gameObject.SetActive(true);
        return novoProduto;
    }

    private Vector3 CalcularPosicaoParadaNpc()
    {
        Vector3 direcaoCliente = ObterDirecaoCliente();
        Vector3 posicao = scanner.transform.position + direcaoCliente * distanciaParadaNoScanner;
        posicao.y = posicaoSpawnNpc.y;
        return posicao;
    }

    private Vector3 CalcularPosicaoSpawnProduto()
    {
        Vector3 direcaoCliente = ObterDirecaoCliente();
        Vector3 posicao = scanner.transform.position + direcaoCliente * distanciaSpawnProduto;
        posicao.y = scanner.transform.position.y + alturaSpawnProduto;
        return posicao;
    }

    private Vector3 ObterDirecaoCliente()
    {
        Vector3 direcao = posicaoSpawnNpc - scanner.transform.position;
        direcao.y = 0f;

        if (direcao.sqrMagnitude <= 0.001f)
        {
            return Vector3.forward;
        }

        return direcao.normalized;
    }

    private bool JogadorPertoDoScanner()
    {
        if (jogador == null)
        {
            return true;
        }

        Vector3 posicaoJogador = jogador.position;
        Vector3 posicaoScanner = scanner.transform.position;
        posicaoJogador.y = 0f;
        posicaoScanner.y = 0f;

        return Vector3.Distance(posicaoJogador, posicaoScanner) <= distanciaEntregaTroco;
    }

    private float SortearValorPago(float precoProduto)
    {
        float[] opcoesValidas = valoresPagamento;
        float[] valoresPossiveis = new float[opcoesValidas.Length];
        int quantidadeValores = 0;

        for (int i = 0; i < opcoesValidas.Length; i++)
        {
            if (opcoesValidas[i] > precoProduto)
            {
                valoresPossiveis[quantidadeValores] = opcoesValidas[i];
                quantidadeValores++;
            }
        }

        if (quantidadeValores == 0)
        {
            return precoProduto + 10f;
        }

        int indiceEscolhido = Random.Range(0, quantidadeValores);
        return valoresPossiveis[indiceEscolhido];
    }

    private string FormatarDinheiro(float valor)
    {
        return valor.ToString("F2").Replace(".", ",");
    }
}
