using UnityEngine;

public class Pickup : MonoBehaviour
{
    public int index;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        GameManager.Instance.CollectPickup(this);
    }
}