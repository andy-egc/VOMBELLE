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
                if (movAurora != null) movAurora.speed++;

                ControlMovimientoLysara movLysara = player.GetComponent<ControlMovimientoLysara>();
                if (movLysara != null) movLysara.speed++;
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
