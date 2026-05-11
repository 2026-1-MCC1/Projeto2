using UnityEngine;

public class PickupProxy : MonoBehaviour
{
    // Referencia para o Pickup principal do produto que deve reagir ao clique.
    [SerializeField] Pickup pickupPrincipal;

    private void Awake()
    {
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
