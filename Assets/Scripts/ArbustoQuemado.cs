using UnityEngine;

[System.Serializable]
public class PowerUpConProbabilidad
{
    public GameObject powerUp;
    [Range(0f, 1f)]
    public float probabilidad = 0.33f;
}

public class ArbustoQuemado : MonoBehaviour
{
    public float destructionTime = 1f;

    [Range(0f, 1f)]
    public float probabilidadGeneral = 0.2f;

    public PowerUpConProbabilidad[] powerUps;

    private void Start()
    {
        Destroy(gameObject, destructionTime);
    }

    private void OnDestroy()
    {
        if (powerUps.Length <= 0) return;
        if (Random.value > probabilidadGeneral) return;

        // Suma total de probabilidades
        float total = 0f;
        foreach (var p in powerUps)
            total += p.probabilidad;

        // Elige un powerup según su peso
        float random = Random.Range(0f, total);
        float acumulado = 0f;

        foreach (var p in powerUps)
        {
            acumulado += p.probabilidad;
            if (random <= acumulado)
            {
                Instantiate(p.powerUp, transform.position, Quaternion.identity);
                return;
            }
        }
    }
}