using UnityEngine;
using UnityEngine.UI;

public class BarraTeste : MonoBehaviour
{
    // Este script e uma barra de progresso simples usada pelo scanner.
    // Ele foi mantido separado para nao misturar a UI antiga da barra com a logica principal do Scanner.
    // Script auxiliar que atualiza uma barra visual sempre que o scanner conta um item.
    // Imagem de UI preenchida para representar o progresso.
    public Image barra;

    // Valor interno usado para controlar o preenchimento da barra.
    float progresso = 0f;
    // Flag numerica mantida para compatibilidade com o Scanner.
    public float collided = 0f;

    private void Update()
    {
        // Se a imagem da barra nao foi ligada no Inspector, o script para aqui para evitar erro.
        if (barra == null)
        {
            return;
        }

        // Aumenta a barra quando outro script registra uma colisao/scan.
        if (collided == 1f)
        {
            // Soma um pequeno passo de progresso para cada produto registrado.
            progresso += 0.1f;
            barra.fillAmount = progresso;
            collided -= 1f;
        }
    }
}
