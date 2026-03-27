using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    // Distância máxima para o jogador interagir com o NPC
    public float interactionDistance = 3f;

    // Referência ao jogador (Transform = posição dele no mundo)
    public Transform player;

    // Mensagem que o NPC vai falar
    public string message = "Olá, cliente! Posso ajudar?";

    void Update()
    {
        // Calcula a distância entre o NPC e o jogador
        float distance = Vector3.Distance(transform.position, player.position);

        // Verifica se o jogador está perto o suficiente
        if (distance <= interactionDistance)
        {
            // Verifica se o jogador apertou a tecla E
            if (Input.GetKeyDown(KeyCode.E))
            {
                // Chama a função de interação
                Interact();
            }
        }
    }

    void Interact()
    {
        // Exibe a mensagem no console (depois você pode trocar por UI)
        Debug.Log("Interação com NPC.");

        // Aqui vocês podem adicionar mais coisas:
        // - abrir diálogo na tela
        // - iniciar missão
        // - tocar som
    }
}
