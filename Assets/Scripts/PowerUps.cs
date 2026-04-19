using UnityEngine;

public class PowerUps : MonoBehaviour
{
    public enum PowerUpType
    {
        BombaExtra,
        RadioExplosion,
        Velocidad,
    }

    public PowerUpType type;
    public float rotacionVelocidad = 90f;

    private void Update()
    {
        transform.Rotate(0f, 0f, rotacionVelocidad * Time.deltaTime);
    }

    private void OnItemPickup(GameObject player)
    {
        switch (type)
        {
            case PowerUpType.BombaExtra:
                player.GetComponent<ControlBomba>().AddBomb();
                break;

            case PowerUpType.RadioExplosion:
                player.GetComponent<ControlBomba>().explosionRadius++;
                break;

            case PowerUpType.Velocidad:
                ControlMovimiento movAurora = player.GetComponent<ControlMovimiento>();
                if (movAurora != null && movAurora.speed < 8f) movAurora.speed++;

                ControlMovimientoLysara movLysara = player.GetComponent<ControlMovimientoLysara>();
                if (movLysara != null && movLysara.speed < 8f) movLysara.speed++;
                break;
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Lysara"))
        {
            OnItemPickup(other.gameObject);
        }
    }
}