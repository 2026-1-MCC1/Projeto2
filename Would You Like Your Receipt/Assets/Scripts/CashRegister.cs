using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Progress;

public class CashRegister : MonoBehaviour
{
    public Text productText;   // texto que mostra o nome do produto
    public Text barcodeText;   // texto que mostra o código
    public Text priceText;     // texto que mostra o preço
    public Text totalText;     // texto que mostra o total

    private float total = 0f;  // variável que guarda o total da compra

    public void ScanItem(Item item)
    {
        // Atualiza o nome do produto na tela
        productText.text = "Produto: " + item.itemName;

        // Mostra o código de barras
        barcodeText.text = "Código: " + item.barcode;

        // Mostra o preço formatado com 2 casas decimais
        priceText.text = "Preço: R$ " + item.price.ToString("F2");

        // Soma o preço do item ao total
        total += item.price;

        // Atualiza o total na tela
        totalText.text = "Total: R$ " + total.ToString("F2");
    }
}
