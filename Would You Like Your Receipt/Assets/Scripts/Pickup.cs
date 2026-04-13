using UnityEngine;

public class Pickup : MonoBehaviour
{
    bool isHolding = false;
    [SerializeField] float throwForce = 150f;
    [SerializeField] float maxDistance = 3f;
    float distance;
    public CameraConsole console; //Permite selecionar GameObject do script no inspetor da Unity para referenciar depois.
    public BarraTeste barraTeste;

    TempParent tempParent;
    Rigidbody rb;

    Vector3 objPosition;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("NPC"))
        {
            barraTeste.collided = barraTeste.collided + 1f;
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        tempParent = TempParent.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        if(isHolding)
        {
            Hold();
        }
    }

    private void OnMouseDown()
    {
        //Pegar
        if (tempParent != null)
        {
            distance = Vector3.Distance(this.transform.position, tempParent.transform.position); //Compara distância do objeto com a posição do jogador para definir a distância entre os dois.
            if (distance <= maxDistance)
            {
                isHolding = true;
                rb.useGravity = false;
                rb.detectCollisions = true;

                this.transform.SetParent(tempParent.transform); //Faz com que o item segurado se torne filho do objeto vazio "TempParent" (Para que não precise aplicar o script em todo item que deseja ser interagível)

            }
        }
        else
        {
            Debug.Log("TempParent item not found in scene!");
        }
    }

    private void OnMouseUp() //Solta item ao soltar botão do mouse.
    {
        Drop();
    }

    private void OnMouseExit() //Solta item ao mouse sair dos confins do objeto.
    {
        Drop();
    }

    private void Hold()
    {
        distance = Vector3.Distance(this.transform.position, tempParent.transform.position); //Compara distância do objeto com a posição do jogador para definir a distância entre os dois.

        if(distance >= maxDistance) //Solta o item se estiver longe demais.
        {
            Drop();
        }

        if(Input.GetKeyDown(console.OpenCameras))
        {
            Drop();
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if(Input.GetMouseButtonDown(1))
        {
            //Joga item se clicar com o botão direito do mouse.
            rb.AddForce(tempParent.transform.forward * throwForce);
            Drop();
        }
    }

    private void Drop() //Função para soltar itens.
    {
        if(isHolding)
        {
            isHolding = false;
            objPosition = this.transform.position;
            this.transform.position = objPosition;
            this.transform.SetParent(null);
            rb.useGravity = true;
        }
    }
}
