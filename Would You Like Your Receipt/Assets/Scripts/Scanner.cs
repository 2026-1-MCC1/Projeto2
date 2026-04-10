using UnityEngine;

public class Scanner : MonoBehaviour
{
    public CashRegister cashRegister; // referência da caixa registradora

    private void OnTriggerEnter(Collider other)
    {
        // tenta pegar o script Item do objeto que entrou no scanner
        Item item = other.GetComponent<Item>();

        // verifica se o objeto realmente é um item
        if (item != null)
        {
            // envia o item para a caixa registradora
            cashRegister.ScanItem(item);

            // destrói o item depois de escanear (simula passar no caixa)
            Destroy(other.gameObject);
        }
    }
}
