using UnityEngine;

public class Explosion : MonoBehaviour
{
    public SpritesAnimadosRender start;
    public SpritesAnimadosRender middle;
    public SpritesAnimadosRender end;

    public void SetActiveRenderer(SpritesAnimadosRender renderer)
    {
        start.enabled = renderer == start;
        middle.enabled = renderer == middle;
        end.enabled = renderer == end;
    }

    //Rotar el sprite dependiendo de la dirrecicon de la explosion
    public void SetDirection(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x);
        transform.rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward);
    }

    public void DestroyAfter(float seconds)
    {
        Destroy(gameObject, seconds);
    }
}
