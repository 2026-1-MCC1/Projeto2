using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class ConfiguradorInteractables : MonoBehaviour
{
    // Define se a configuracao deve acontecer automaticamente ao carregar ou mudar a hierarquia.
    [SerializeField] bool configurarAutomaticamente = true;
    // Inclui filhos desligados para que objetos importados tambem recebam configuracao completa.
    [SerializeField] bool incluirFilhosInativos = true;
    // Mantem os produtos-base presos no lugar para servirem apenas como molde do item do caixa.
    [SerializeField] bool manterProdutosBaseParados = true;
    // Usa gravidade apenas quando voce realmente quiser que os produtos-base da hierarquia caiam na cena.
    [SerializeField] bool usarGravidadeNosProdutosBase = false;

    private void OnEnable()
    {
        // Tenta configurar a prateleira sempre que este objeto ficar ativo.
        TentarConfigurar();
    }

    private void OnValidate()
    {
        // Reaplica a configuracao quando algum valor mudar no Inspector.
        TentarConfigurar();
    }

    private void OnTransformChildrenChanged()
    {
        // Atualiza automaticamente quando novos produtos forem colocados dentro de Interactables.
        TentarConfigurar();
    }

    [ContextMenu("Configurar Interactables")]
    public void ConfigurarInteractables()
    {
        bool houveMudanca = false;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform raizProduto = transform.GetChild(i);
            if (raizProduto == null)
            {
                continue;
            }

            Pickup pickupPrincipal = raizProduto.GetComponent<Pickup>();
            if (pickupPrincipal == null)
            {
                // Garante um Pickup na raiz do produto para o scanner encontrar sempre o mesmo componente.
                pickupPrincipal = raizProduto.gameObject.AddComponent<Pickup>();
                houveMudanca = true;
            }

            // Marca explicitamente qual Transform representa o produto inteiro.
            pickupPrincipal.DefinirRaizDoProduto(raizProduto);

            Collider[] colliders = GarantirCollidersDoProduto(raizProduto, ref houveMudanca);
            GarantirRigidbodyDoProduto(raizProduto, ref houveMudanca);

            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider colliderAtual = colliders[colliderIndex];
                if (colliderAtual == null || colliderAtual.gameObject == raizProduto.gameObject)
                {
                    continue;
                }

                PickupProxy proxy = colliderAtual.GetComponent<PickupProxy>();
                if (proxy == null)
                {
                    // Coloca um proxy em cada collider filho para encaminhar os cliques ao Pickup principal.
                    proxy = colliderAtual.gameObject.AddComponent<PickupProxy>();
                    houveMudanca = true;
                }

                proxy.Configurar(pickupPrincipal);
            }
        }

#if UNITY_EDITOR
        if (houveMudanca && !Application.isPlaying)
        {
            // Marca a cena como alterada para a Unity pedir salvamento das configuracoes novas.
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    void TentarConfigurar()
    {
        if (!configurarAutomaticamente && !Application.isPlaying)
        {
            return;
        }

        // Executa a configuracao central em todos os momentos importantes do ciclo de edicao e play.
        ConfigurarInteractables();
    }

    Collider[] GarantirCollidersDoProduto(Transform raizProduto, ref bool houveMudanca)
    {
        Collider[] colliders = raizProduto.GetComponentsInChildren<Collider>(incluirFilhosInativos);
        if (colliders.Length == 0)
        {
            Renderer[] renderers = raizProduto.GetComponentsInChildren<Renderer>(incluirFilhosInativos);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rendererAtual = renderers[i];
                if (rendererAtual == null)
                {
                    continue;
                }

                if (rendererAtual.GetComponent<Collider>() != null)
                {
                    continue;
                }

                // Cria collider nos objetos que realmente possuem malha visivel para melhorar a colisao.
                rendererAtual.gameObject.AddComponent<BoxCollider>();
                houveMudanca = true;
            }

            colliders = raizProduto.GetComponentsInChildren<Collider>(incluirFilhosInativos);
            if (colliders.Length == 0)
            {
                // Fallback final para modelos sem renderer ou malha exposta.
                raizProduto.gameObject.AddComponent<BoxCollider>();
                colliders = raizProduto.GetComponentsInChildren<Collider>(incluirFilhosInativos);
                houveMudanca = true;
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                // Mantem os colliders ativos e fisicos para os itens responderem ao mundo.
                colliders[i].enabled = true;
                colliders[i].isTrigger = false;
            }
        }

        return colliders;
    }

    void GarantirRigidbodyDoProduto(Transform raizProduto, ref bool houveMudanca)
    {
        Rigidbody rigidbodyPrincipal = raizProduto.GetComponent<Rigidbody>();
        if (rigidbodyPrincipal == null)
        {
            rigidbodyPrincipal = raizProduto.GetComponentInChildren<Rigidbody>(incluirFilhosInativos);
        }

        if (rigidbodyPrincipal == null)
        {
            // Cria um Rigidbody principal quando o modelo ainda nao tiver fisica dinamica.
            rigidbodyPrincipal = raizProduto.gameObject.AddComponent<Rigidbody>();
            houveMudanca = true;
        }

        if (manterProdutosBaseParados)
        {
            // O produto-base fica travado no lugar para nao sair voando nem cair da area onde voce organizou.
            if (!rigidbodyPrincipal.isKinematic)
            {
                rigidbodyPrincipal.linearVelocity = Vector3.zero;
                rigidbodyPrincipal.angularVelocity = Vector3.zero;
            }

            rigidbodyPrincipal.isKinematic = true;
            rigidbodyPrincipal.useGravity = usarGravidadeNosProdutosBase;
            rigidbodyPrincipal.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbodyPrincipal.interpolation = RigidbodyInterpolation.None;
        }
        else
        {
            // Este modo deixa o produto-base fisico caso voce realmente queira ele solto na cena.
            rigidbodyPrincipal.isKinematic = false;
            rigidbodyPrincipal.useGravity = true;
            rigidbodyPrincipal.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbodyPrincipal.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        rigidbodyPrincipal.detectCollisions = true;
    }
}
