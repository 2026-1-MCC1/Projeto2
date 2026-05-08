using UnityEngine;

public class AutoShopCollision : MonoBehaviour
{
    // Nome do objeto visual principal da loja dentro do StoreModel.
    [SerializeField] string nomeModeloLoja = "shop";
    // Inclui filhos desligados, caso alguma parte da loja comece desativada no Editor.
    [SerializeField] bool incluirFilhosInativos = true;
    // MeshCollider concavo e o correto para loja/cenario estatico com interior acessivel.
    [SerializeField] bool usarMeshColliderConcavo = true;
    // Evita repetir a varredura de meshes quando Awake e SistemaCaixa chamam o preparo no mesmo Play.
    bool colisoesPreparadas;

    private void Awake()
    {
        // Prepara a colisao automaticamente quando o script estiver na cena.
        PrepararColisoesDaLoja();
    }

    public void PrepararColisoesDaLoja()
    {
        if (colisoesPreparadas)
        {
            return;
        }

        Transform alvo = EncontrarModeloLoja();
        if (alvo == null)
        {
            Debug.LogWarning("AutoShopCollision nao encontrou StoreModel/shop para criar colisoes.");
            return;
        }

        // Remove BoxColliders grandes do modelo da loja, porque eles fecham o interior.
        RemoverBoxCollidersDoModelo(alvo);

        MeshFilter[] meshes = alvo.GetComponentsInChildren<MeshFilter>(incluirFilhosInativos);
        int totalCriado = 0;

        for (int i = 0; i < meshes.Length; i++)
        {
            MeshFilter meshFilter = meshes[i];
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            if (DeveIgnorarMesh(meshFilter.transform))
            {
                continue;
            }

            MeshCollider collider = meshFilter.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                totalCriado++;
            }

            // Usa a propria geometria do mesh para a colisao nao virar um bloco fechado.
            collider.sharedMesh = meshFilter.sharedMesh;
            collider.convex = !usarMeshColliderConcavo;
            collider.isTrigger = false;
            collider.enabled = true;
        }

        // Garante que o objeto raiz tambem tenha collider quando a loja for um unico mesh.
        MeshFilter meshRaiz = alvo.GetComponent<MeshFilter>();
        if (meshRaiz != null && meshRaiz.sharedMesh != null && !DeveIgnorarMesh(alvo))
        {
            MeshCollider colliderRaiz = alvo.GetComponent<MeshCollider>();
            if (colliderRaiz == null)
            {
                colliderRaiz = alvo.gameObject.AddComponent<MeshCollider>();
            }

            colliderRaiz.sharedMesh = meshRaiz.sharedMesh;
            colliderRaiz.convex = !usarMeshColliderConcavo;
            colliderRaiz.isTrigger = false;
            colliderRaiz.enabled = true;
        }

        colisoesPreparadas = true;
    }

    private Transform EncontrarModeloLoja()
    {
        // Se o script estiver colocado no proprio shop ou StoreModel, tenta resolver a partir daqui.
        Transform shopLocal = EncontrarFilhoPorNome(transform, nomeModeloLoja);
        if (shopLocal != null)
        {
            return shopLocal;
        }

        if (transform.name.ToLowerInvariant() == nomeModeloLoja.ToLowerInvariant())
        {
            return transform;
        }

        GameObject storeModel = GameObject.Find("StoreModel");
        if (storeModel != null)
        {
            Transform shopNoStore = EncontrarFilhoPorNome(storeModel.transform, nomeModeloLoja);
            if (shopNoStore != null)
            {
                return shopNoStore;
            }

            // Fallback: se nao existir filho chamado shop, usa o StoreModel inteiro.
            return storeModel.transform;
        }

        GameObject shop = GameObject.Find(nomeModeloLoja);
        return shop != null ? shop.transform : null;
    }

    private Transform EncontrarFilhoPorNome(Transform raiz, string nome)
    {
        Transform[] filhos = raiz.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            if (filhos[i].name.ToLowerInvariant() == nome.ToLowerInvariant())
            {
                return filhos[i];
            }
        }

        return null;
    }

    private void RemoverBoxCollidersDoModelo(Transform alvo)
    {
        BoxCollider[] boxColliders = alvo.GetComponentsInChildren<BoxCollider>(incluirFilhosInativos);
        for (int i = 0; i < boxColliders.Length; i++)
        {
            if (DeveIgnorarMesh(boxColliders[i].transform))
            {
                continue;
            }

            // Desliga box collider de cenario para nao bloquear a parte interna da loja.
            boxColliders[i].enabled = false;
        }
    }

    private bool DeveIgnorarMesh(Transform item)
    {
        // Nao mexe nos produtos/interativos; eles usam colliders proprios.
        Transform atual = item;
        while (atual != null)
        {
            string nome = atual.name.ToLowerInvariant();
            if (nome.Contains("interactables") || nome.Contains("spawnobjects"))
            {
                return true;
            }

            if (atual.GetComponent<Pickup>() != null)
            {
                return true;
            }

            atual = atual.parent;
        }

        return false;
    }
}
