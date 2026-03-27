using UnityEngine;

public class TempParent : MonoBehaviour
{
    public static TempParent Instance { get; private set; }

    private void Awake() //Garante que apenas um objeto exista abaixo de "TempParent"
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }
}
