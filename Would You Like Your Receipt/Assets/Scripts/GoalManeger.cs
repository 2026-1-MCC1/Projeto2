using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoalManager : MonoBehaviour
{
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

    private void Awake()
    {
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
        clientesAtendidos++;
        AtualizarInterface();
        ChecarObjetivos();
    }

    // CHAMAR quando vender produto
    public void ProdutoVendido()
    {
        produtosVendidos++;
        AtualizarInterface();
        ChecarObjetivos();
    }

    public void MostrarMensagem(string mensagem, float duracao = 2f)
    {
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
            Debug.Log("OBJETIVOS COMPLETOS!");
        }
    }
}
