using UnityEngine;
using UnityEngine.UI;

public class BarraTeste : MonoBehaviour
{
    public Image barra;

    float progresso = 0f;
    public float collided = 0f;

    void Update()
    {
        // Aperta enter pra aumentar a barra
        if (collided == 1f)
        {
            progresso += 0.1f;
            barra.fillAmount = progresso;
            collided -= 1f;
        }
    }
}