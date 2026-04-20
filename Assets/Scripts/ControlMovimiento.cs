using UnityEngine;

public class ControlMovimiento : MonoBehaviour
{
    public new Rigidbody2D rigidbody { get; private set; }
    private Vector2 direction = Vector2.down;
    public float speed = 5f;

    public KeyCode inputUp = KeyCode.W;
    public KeyCode inputDown = KeyCode.S;
    public KeyCode inputLeft = KeyCode.A;
    public KeyCode inputRight = KeyCode.D;

    public SpritesAnimadosRender spriteRendererUp;
    public SpritesAnimadosRender spriteRendererDown;
    public SpritesAnimadosRender spriteRendererLeft;
    public SpritesAnimadosRender spriteRendererRight;

    [Header("Vidas")]
    public int Vidas = 3;
    private bool isInvincible = false;

    public SpritesAnimadosRender spriteRenderDeath;
    public SpritesAnimadosRender spriteRenderDamage;
    private SpritesAnimadosRender activeSpriteRenderer;

    public void Awake()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;
    }

    private void Update()
    {
        if (Input.GetKey(inputUp))
            SetDirection(Vector2.up, spriteRendererUp);
        else if (Input.GetKey(inputDown))
            SetDirection(Vector2.down, spriteRendererDown);
        else if (Input.GetKey(inputLeft))
            SetDirection(Vector2.left, spriteRendererLeft);
        else if (Input.GetKey(inputRight))
            SetDirection(Vector2.right, spriteRendererRight);
        else
            SetDirection(Vector2.zero, activeSpriteRenderer);
    }

    private void FixedUpdate()
    {
        Vector2 position = rigidbody.position;
        Vector2 translation = direction * speed * Time.fixedDeltaTime;
        rigidbody.MovePosition(position + translation);
    }

    private void SetDirection(Vector2 newDirection, SpritesAnimadosRender spriteRenderer)
    {
        direction = newDirection;

        spriteRendererUp.enabled = spriteRenderer == spriteRendererUp;
        spriteRendererDown.enabled = spriteRenderer == spriteRendererDown;
        spriteRendererLeft.enabled = spriteRenderer == spriteRendererLeft;
        spriteRendererRight.enabled = spriteRenderer == spriteRendererRight;

        activeSpriteRenderer = spriteRenderer;
        activeSpriteRenderer.idle = direction == Vector2.zero;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isInvincible) return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            Vidas--;
            if (Vidas >= 1)
                DamageSequence();
            else
                DeathSequence();
        }
    }

    private void DamageSequence()
    {
        isInvincible = true;
        enabled = false;
        speed = 5f;
        GetComponent<ControlBomba>().enabled = false;

        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;

        spriteRenderDamage.ResetAnimation();
        spriteRenderDamage.enabled = true;

        Invoke(nameof(OnDamageSequenceEnded), 2f);
    }

    private void OnDamageSequenceEnded()
    {
        isInvincible = false;
        enabled = true;
        ControlBomba cb = GetComponent<ControlBomba>();
        cb.enabled = true;
        //(cb.ResetPowerUps(); // Vuelve a los valores originales

        spriteRenderDamage.ResetAnimation();
        spriteRenderDamage.enabled = false;

        spriteRendererDown.enabled = true;
        activeSpriteRenderer = spriteRendererDown;
        activeSpriteRenderer.idle = true;
    }

    private void DeathSequence()
    {
        isInvincible = true;
        enabled = false;
        GetComponent<ControlBomba>().enabled = false;

        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;

        spriteRenderDeath.ResetAnimation();
        spriteRenderDeath.enabled = true;

        Invoke(nameof(OnDeathSequenceEnded), 3.25f);
    }

    private void OnDeathSequenceEnded()
    {
        gameObject.SetActive(false);
    }
}