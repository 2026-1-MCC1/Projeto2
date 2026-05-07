using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Scanner : MonoBehaviour
{
    public static event Action<Pickup> ProdutoEscaneado;

    // Referencia opcional para a barra antiga de teste.
    [SerializeField] BarraTeste barraTeste;
    // Texto do Canvas usado para mostrar os dados do produto escaneado.
    [SerializeField] TMP_Text textoInfoProduto;

    GoalManager goalManager;
    Coroutine limparTextoInfoProdutoCoroutine;

    private void Awake()
    {
        goalManager = GoalManager.Instance;
        if (goalManager == null)
        {
            // Procura o GoalManager automaticamente caso ele nao esteja ligado ainda.
            goalManager = UnityEngine.Object.FindFirstObjectByType<GoalManager>();
        }

        if (barraTeste == null)
        {
            // Procura a barra antiga para manter compatibilidade com a cena atual.
            barraTeste = UnityEngine.Object.FindFirstObjectByType<BarraTeste>();
        }

        if (textoInfoProduto == null)
        {
            // Procura automaticamente o texto do scanner dentro do Canvas.
            GameObject textoScannerObject = GameObject.Find("TextoScanner");
            if (textoScannerObject != null)
            {
                textoInfoProduto = textoScannerObject.GetComponent<TMP_Text>();
            }
        }

        if (goalManager == null)
        {
            Debug.LogWarning("GoalManager nao foi encontrado para o Scanner.", this);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Permite ler itens ao encostar em um scanner com collider comum.
        TryScan(collision.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Permite ler itens ao passar por um scanner configurado como trigger.
        TryScan(other.transform);
    }

    private void TryScan(Transform otherTransform)
    {
        Pickup pickup = otherTransform.GetComponentInParent<Pickup>();
        if (pickup == null)
        {
            return;
        }

        if (!pickup.TentarIniciarEscaneamento())
        {
            return;
        }

        // Atualiza a barra antiga, caso ela ainda esteja em uso na cena.
        if (barraTeste != null)
        {
            barraTeste.collided = barraTeste.collided + 1f;
        }

        if (goalManager != null)
        {
            // Conta o produto escaneado.
            goalManager.ProdutoVendido();
        }

        if (textoInfoProduto != null)
        {
            // Mostra os dados do produto no texto dedicado do Canvas.
            MostrarTextoInfoProduto(pickup.ObterMensagemEscaneamento(), pickup.ObterDuracaoMensagemEscaneamento());
        }
        else if (goalManager != null)
        {
            // Usa o texto de objetivos como fallback se o texto do scanner nao existir.
            goalManager.MostrarMensagem(pickup.ObterMensagemEscaneamento(), pickup.ObterDuracaoMensagemEscaneamento());
        }

        ProdutoEscaneado?.Invoke(pickup);
        pickup.FinalizarEscaneamento();
    }

    public void MostrarTextoInfoProduto(string texto, float duracao = 0f)
    {
        if (textoInfoProduto == null)
        {
            return;
        }

        textoInfoProduto.text = texto;

        if (limparTextoInfoProdutoCoroutine != null)
        {
            StopCoroutine(limparTextoInfoProdutoCoroutine);
            limparTextoInfoProdutoCoroutine = null;
        }

        if (duracao > 0f)
        {
            // Agenda a limpeza automatica apenas quando uma duracao foi informada.
            limparTextoInfoProdutoCoroutine = StartCoroutine(LimparTextoInfoProdutoDepois(duracao));
        }
    }

    public void LimparTextoInfoProduto()
    {
        if (limparTextoInfoProdutoCoroutine != null)
        {
            StopCoroutine(limparTextoInfoProdutoCoroutine);
            limparTextoInfoProdutoCoroutine = null;
        }

        if (textoInfoProduto != null)
        {
            textoInfoProduto.text = string.Empty;
        }
    }

    private IEnumerator LimparTextoInfoProdutoDepois(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        // Limpa o texto do scanner depois de alguns segundos.
        LimparTextoInfoProduto();
    }
}
