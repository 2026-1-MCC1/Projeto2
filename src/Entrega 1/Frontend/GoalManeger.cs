using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoalManager : MonoBehaviour
{
    // OBJETIVOS
    public int clientesObjetivo = 10;
    public int produtosObjetivo = 5;

    // PROGRESSO
    private int clientesAtendidos = 0;
    private int produtosVendidos = 0;

    // UI (texto na tela)
    public TMP_Text textoObjetivos;

    // BARRA DE PROGRESSO (opcional)
    public Image barraProgresso;

    void Start()
    {
        AtualizarUI();
        barraProgresso.fillAmount = 0.5f;
    }

    // CHAMAR quando atender cliente
    public void ClienteAtendido()
    {
        clientesAtendidos++;
        AtualizarUI();
        ChecarObjetivos();
    }

    // CHAMAR quando vender produto
    public void ProdutoVendido()
    {
        produtosVendidos++;
        AtualizarUI();
        ChecarObjetivos();
    }

    void AtualizarUI()
    {
        textoObjetivos.text =
            "Clientes: " + clientesAtendidos + "/" + clientesObjetivo +
            "\nProdutos: " + produtosVendidos + "/" + produtosObjetivo;
    }

    void ChecarObjetivos()
    {
        if (clientesAtendidos >= clientesObjetivo &&
            produtosVendidos >= produtosObjetivo)
        {
            Debug.Log("OBJETIVOS COMPLETOS!");
        }
    }
}