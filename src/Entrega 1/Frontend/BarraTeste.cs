using UnityEngine;
using UnityEngine.UI;

public class BarraTeste : MonoBehaviour
{
    public Image barra;

    float progresso = 0f;

    void Update()
    {
        // Aperta espaço pra aumentar a barra
        if (Input.GetKeyDown(KeyCode.Space))
        {
            progresso += 0.1f;
            barra.fillAmount = progresso;
        }
    }
}