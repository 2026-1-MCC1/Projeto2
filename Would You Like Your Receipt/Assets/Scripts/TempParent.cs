using UnityEngine;

public class TempParent : MonoBehaviour
{
    // Este objeto funciona como ancora temporaria para itens carregados pelo jogador.
    // Referencia global do ponto onde os objetos segurados ficam presos.
    public static TempParent Instance { get; private set; }

    private void Awake()
    {
        // Garante que exista apenas um TempParent ativo na cena.
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }
}
