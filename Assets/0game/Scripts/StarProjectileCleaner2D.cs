using UnityEngine;

public class StarProjectileCleaner2D : MonoBehaviour
{
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<StarProjectile>(out var projectile))
        {
            Destroy(projectile.gameObject);
        }
    }
}
