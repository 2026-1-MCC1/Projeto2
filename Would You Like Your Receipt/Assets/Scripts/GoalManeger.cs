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

    }

    void Update()
    {

    }

    // CHAMAR quando atender cliente
    public void ClienteAtendido()
    {
        clientesAtendidos++;
        ChecarObjetivos();
    }

    // CHAMAR quando vender produto
    public void ProdutoVendido()
    {
        produtosVendidos++;
        ChecarObjetivos();
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