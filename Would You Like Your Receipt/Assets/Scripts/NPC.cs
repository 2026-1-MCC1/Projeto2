using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    // Distância máxima para o jogador interagir com o NPC
    public float interactionDistance = 3f;

    // Referência ao jogador (Transform = posição dele no mundo)
    public Transform player;

    // Mensagem que o NPC vai falar
    public string message = "Olá, pode me ajudar?";
    public float messageDuration = 2f;

    // Guarda a referencia para a UI que mostra as mensagens na tela.
    private GoalManager goalManager;

    private void Start()
    {
        if (player == null)
        {
            // Procura o jogador automaticamente caso ele nao tenha sido ligado no Inspector.
            PlayerMovement playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
            {
                player = playerMovement.transform;
            }
        }

        goalManager = GoalManager.Instance;
        if (goalManager == null)
        {
            // Faz uma busca de seguranca caso a instancia ainda nao esteja preenchida.
            goalManager = Object.FindFirstObjectByType<GoalManager>();
        }
    }

    void Update()
    {
        if (player == null)
        {
            return;
        }

        // Calcula a distância entre o NPC e o jogador
        float distance = Vector3.Distance(transform.position, player.position);

        // Verifica se o jogador está perto o suficiente
        if (distance <= interactionDistance)
        {
            // Verifica se o jogador apertou a tecla E
            if (Input.GetKeyDown(KeyCode.E))
            {
                Interact();
            }
        }
    }

    void Interact()
    {
        if (goalManager != null)
        {
            // Mostra a fala do NPC no mesmo texto usado pelos objetivos.
            goalManager.MostrarMensagem(message, messageDuration);
        }
        else
        {
            Debug.LogWarning("GoalManager was not found for NPCInteraction.", this);
        }

        // Mantem a mensagem no Console para facilitar debug.
        Debug.Log(message);
    }
}
