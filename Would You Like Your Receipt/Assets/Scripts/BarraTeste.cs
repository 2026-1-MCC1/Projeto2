using UnityEngine;
using UnityEngine.UI;

public class BarraTeste : MonoBehaviour
{
    // Imagem de UI preenchida para representar o progresso.
    public Image barra;

    // Valor interno usado para controlar o preenchimento da barra.
    float progresso = 0f;
    // Flag numerica mantida para compatibilidade com o Scanner.
    public float collided = 0f;

    void Update()
    {
        // Aumenta a barra quando outro script registra uma colisao/scan.
        if (collided == 1f)
        {
            progresso += 0.1f;
            barra.fillAmount = progresso;
            collided -= 1f;
        }
    }
}
