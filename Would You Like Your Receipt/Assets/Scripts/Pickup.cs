using System.Globalization;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    bool estaSegurando = false;
    bool foiEscaneado = false;

    [SerializeField] float throwForce = 150f;
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

    TempParent tempParent;
    Rigidbody rb;
    Vector3 objPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
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
            if (distance <= maxDistance)
            {
                estaSegurando = true;
                rb.useGravity = false;
                rb.detectCollisions = true;
                transform.SetParent(tempParent.transform);
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

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Joga o item para frente ao clicar com o botao direito.
        if (Input.GetMouseButtonDown(1))
        {
            rb.AddForce(tempParent.transform.forward * throwForce);
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
        return string.IsNullOrWhiteSpace(nomeProduto) ? gameObject.name : nomeProduto;
    }

    public float ObterPrecoProduto()
    {
        return precoProduto;
    }

    public float ObterDuracaoMensagemEscaneamento()
    {
        return duracaoMensagemEscaneamento;
    }

    public void FinalizarEscaneamento()
    {
        if (destruirAposEscanear)
        {
            // Remove o item da cena depois do escaneamento, se essa opcao estiver ligada.
            Destroy(gameObject);
        }
    }

    private void Soltar()
    {
        // Solta o item e devolve gravidade para ele.
        if (estaSegurando)
        {
            estaSegurando = false;
            objPosition = transform.position;
            transform.position = objPosition;
            transform.SetParent(null);
            rb.useGravity = true;
        }
    }
}
