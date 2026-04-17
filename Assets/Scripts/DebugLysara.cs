using UnityEngine;

public class DebugLysara : MonoBehaviour
{
    private LayerMask maskIndestructible;
    private LayerMask maskDestructible;

    private void Start()
    {
        maskIndestructible = LayerMask.GetMask("Indestructibles");
        maskDestructible   = LayerMask.GetMask("Destructibles");

        // Imprime los valores de las masks para verificar que no sean 0
        Debug.Log($"maskIndestructible = {maskIndestructible.value}");
        Debug.Log($"maskDestructible = {maskDestructible.value}");
    }

    private void Update()
    {
        Vector2 pos = new Vector2(
            Mathf.Round(transform.position.x),
            Mathf.Round(transform.position.y));

        bool caminable = !Physics2D.OverlapBox(pos, Vector2.one * 0.5f, 0f,
            maskIndestructible | maskDestructible);

        bool peligro = GameObject.FindGameObjectsWithTag("Bomba").Length > 0;

        Debug.Log($"Pos={pos} | Caminable={caminable} | BombaActiva={peligro}");
    }
}