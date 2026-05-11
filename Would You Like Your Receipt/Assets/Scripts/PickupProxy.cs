using UnityEngine;

public class PickupProxy : MonoBehaviour
{
    // Este script fica em colliders filhos de um produto.
    // Ele encaminha o clique para o Pickup principal da raiz, evitando que partes do modelo nao sejam clicaveis.
    // Referencia para o Pickup principal do produto que deve reagir ao clique.
    [SerializeField] Pickup pickupPrincipal;

    private void Awake()
    {
        // Se ninguem configurou o proxy, tenta descobrir automaticamente o Pickup pai.
        if (pickupPrincipal == null)
        {
            // Recupera o Pickup na hierarquia caso o campo ainda nao tenha sido ligado.
            pickupPrincipal = GetComponentInParent<Pickup>();
        }
    }

    public void Configurar(Pickup pickup)
    {
        // Liga este proxy ao Pickup central do objeto.
        pickupPrincipal = pickup;
    }

    private void OnMouseDown()
    {
        // Encaminha o clique do collider filho para o Pickup principal.
        if (pickupPrincipal != null)
        {
            pickupPrincipal.IniciarSegurarPeloProxy();
        }
    }

    private void OnMouseUp()
    {
        // Encaminha a soltura do mouse para o Pickup principal.
        if (pickupPrincipal != null)
        {
            pickupPrincipal.SoltarPeloProxy();
        }
    }

    private void OnMouseExit()
    {
        // Nao solta no OnMouseExit para permitir arrastar o item com colliders filhos.
    }
}
