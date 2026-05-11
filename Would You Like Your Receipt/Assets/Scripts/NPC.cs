using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    // Este script serve para NPCs parados/interagiveis da cena.
    // Os clientes automaticos do caixa desativam este componente para nao conflitar com a fila.
    // Script de interacao para NPCs fixos da cena.
    // Distancia maxima para o jogador interagir com o NPC.
    public float interactionDistance = 3f;

    // Referencia ao jogador para medir a distancia no mundo.
    public Transform player;

    // Mensagem que o NPC vai falar quando o jogador apertar E.
    public string message = "Ola, pode me ajudar?";
    public float messageDuration = 2f;

    // Guarda a referencia para a UI que mostra as mensagens na tela.
    private GoalManager goalManager;

    private void Start()
    {
        // No Start, o script encontra as referencias que precisa para funcionar.
        // Isso evita depender de todos os campos preenchidos manualmente no Inspector.
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

    private void Update()
    {
        // A cada frame, verifica se o jogador esta perto e apertou E.
        if (player == null)
        {
            return;
        }

        // Calcula a distancia entre o NPC e o jogador.
        float distance = Vector3.Distance(transform.position, player.position);

        // Verifica se o jogador esta perto o suficiente.
        if (distance <= interactionDistance)
        {
            // Verifica se o jogador apertou a tecla E.
            if (Input.GetKeyDown(KeyCode.E))
            {
                Interact();
            }
        }
    }

    private void Interact()
    {
        // Mostra a mensagem do NPC na UI e tambem no Console da Unity para debug.
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
