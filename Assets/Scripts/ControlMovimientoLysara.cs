using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControlMovimientoLysara : MonoBehaviour
{
    public new Rigidbody2D rigidbody { get; private set; }
    public float speed = 5f;

    public SpritesAnimadosRender spriteRendererUp;
    public SpritesAnimadosRender spriteRendererDown;
    public SpritesAnimadosRender spriteRendererLeft;
    public SpritesAnimadosRender spriteRendererRight;
    public SpritesAnimadosRender spriteRenderDeath;
    public SpritesAnimadosRender spriteRenderDamage; // ← NUEVO
    private SpritesAnimadosRender activeSpriteRenderer;

    [Header("IA Settings")]
    public Transform Aurora;
    public float delayInicio = 3f;
    public float timeBetweenMoves = 0.3f;
    public float bombChance = 0.9f;

    // ← NUEVO bloque de vidas
    [Header("Vidas")]
    public int Vidas = 3;
    private bool isInvincible = false;

    public enum EstadoIA { Idle, Perseguir, Huir, ColocarBomba, RecogerPowerup }
    public EstadoIA estado = EstadoIA.Idle;

    private ControlBomba controlBomba;
    private bool iaActiva = false;

    private Vector2 posicionAnterior;
    private float tiempoSinMover = 0.2f;
    private const float LIMITE_ATASCO = 1.5f;

    private const float CELDA = 1f;
    private const float UMBRAL_LLEGADA = 0.05f;

    private LayerMask maskExplosion;
    private LayerMask maskIndestructible;
    private LayerMask maskDestructible;
    private LayerMask maskTodo;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        controlBomba = GetComponent<ControlBomba>();
        activeSpriteRenderer = spriteRendererDown;
        maskExplosion = LayerMask.GetMask("Explosion");
        maskIndestructible = LayerMask.GetMask("TilesIndestructibles");
        maskDestructible = LayerMask.GetMask("TilesDestructibles");
        maskTodo = maskIndestructible | maskDestructible;
    }


    private void Start()
    {
        StartCoroutine(EsperarInicio());
    }

    private IEnumerator EsperarInicio()
    {
        rigidbody.position = SnappedPosition(rigidbody.position);
        yield return new WaitForSeconds(delayInicio);
        posicionAnterior = rigidbody.position;
        iaActiva = true;
        StartCoroutine(AILoop());
    }

    // ════════════════════════════════════════════════════════
    // AI LOOP
    // ════════════════════════════════════════════════════════

    private IEnumerator AILoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(timeBetweenMoves);
            if (!iaActiva || !enabled) yield break;

            // PRIORIDAD 0 — Desatascar
            float distMovida = Vector2.Distance(rigidbody.position, posicionAnterior);
            if (distMovida < 0.1f)
            {
                tiempoSinMover += timeBetweenMoves;
                if (tiempoSinMover >= LIMITE_ATASCO)
                {
                    tiempoSinMover = 0f;
                    yield return StartCoroutine(Desatascar());
                    posicionAnterior = rigidbody.position;
                    continue;
                }
            }
            else
            {
                tiempoSinMover = 0f;
            }
            posicionAnterior = rigidbody.position;

            // PRIORIDAD 1 — Huir si hay peligro
            if (HayPeligroCerca())
            {
                estado = EstadoIA.Huir;
                yield return StartCoroutine(Huir());

                // NUEVO: espera hasta que no haya peligro antes de continuar
                yield return StartCoroutine(EsperarSeguridad());
                continue;
            }

            // PRIORIDAD 2 — Poner bomba
            if (controlBomba != null && controlBomba.enabled)
            {
                Vector2 celdaActual = SnappedPosition(transform.position);
                bool debePonerBomba = false;

                if (AuroraEnLineaDeFuego(celdaActual))
                    debePonerBomba = true;

                if (!debePonerBomba && HayDestructibleHaciaAurora())
                    debePonerBomba = true;

                if (!debePonerBomba && Aurora != null)
                {
                    float dist = Vector2.Distance(transform.position, Aurora.position);
                    if (dist <= controlBomba.explosionRadius * CELDA + 0.5f)
                        debePonerBomba = true;
                }

                if (debePonerBomba && Random.value < bombChance)
                {
                    Vector2 escape = EncontrarEscape(celdaActual, controlBomba.explosionRadius);
                    float distEscape = Vector2.Distance(celdaActual, escape);

                    if (escape != celdaActual && distEscape >= 2f)
                    {
                        estado = EstadoIA.ColocarBomba;
                        controlBomba.TryPlaceBomb();
                        yield return new WaitForSeconds(0.05f);
                        yield return StartCoroutine(MoverATile(escape));
                        estado = EstadoIA.Huir;
                        yield return StartCoroutine(Huir());

                        // NUEVO: espera quieta hasta que explote y desaparezca la explosión
                        yield return StartCoroutine(EsperarSeguridad());
                        continue;
                    }
                }
            }

            // PRIORIDAD 3 — Recoger powerup
            GameObject powerup = BuscarPowerup(5f);
            if (powerup != null)
            {
                estado = EstadoIA.RecogerPowerup;
                Vector2 siguiente = ObtenerSiguientePasoBFS(
                    SnappedPosition(transform.position),
                    SnappedPosition(powerup.transform.position));
                if (siguiente != Vector2.zero)
                {
                    yield return StartCoroutine(MoverATile(siguiente));
                    continue;
                }
            }

            // PRIORIDAD 4 — Perseguir a Aurora
            if (Aurora != null)
            {
                estado = EstadoIA.Perseguir;
                Vector2 objetivo = BuscarPosicionCercanaAurora();
                Vector2 siguiente = ObtenerSiguientePasoBFS(
                    SnappedPosition(transform.position), objetivo);

                if (siguiente != Vector2.zero)
                {
                    yield return StartCoroutine(MoverATile(siguiente));
                    continue;
                }
            }

            // PRIORIDAD 5 — Movimiento aleatorio
            Vector2 dir4 = DireccionAleatoria();
            Vector2 candidata = SnappedPosition(transform.position) + dir4;
            if (EsCeldaCaminable(candidata))
                yield return StartCoroutine(MoverATile(candidata));
        }
    }

    // Espera quieta hasta que no haya bombas ni explosiones cerca
    // Espera quieta hasta que no haya bombas ni explosiones cerca
    private IEnumerator EsperarSeguridad()
    {
        // Timeout de seguridad: máximo espera el tiempo de mecha + duración explosión
        float tiempoMaxEspera = controlBomba != null
            ? controlBomba.bombFuseTime + controlBomba.explosionDuration + 0.5f
            : 5f;

        float tiempoEsperando = 0f;

        while (tiempoEsperando < tiempoMaxEspera)
        {
            // Si ya no hay peligro, sale inmediatamente
            if (!HayPeligroCerca()) yield break;

            // Si sigue en peligro, intenta moverse a celda más segura
            yield return StartCoroutine(Huir());

            tiempoEsperando += timeBetweenMoves;
            yield return new WaitForSeconds(timeBetweenMoves);
        }
    }

    // ════════════════════════════════════════════════════════
    // POSICIÓN CERCANA A AURORA
    // ════════════════════════════════════════════════════════

    private Vector2 BuscarPosicionCercanaAurora()
    {
        if (Aurora == null) return SnappedPosition(transform.position);

        Vector2 posAurora = SnappedPosition(Aurora.position);
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        Vector2 mejorPos = posAurora;
        float menorDist = Mathf.Infinity;

        for (int r = 1; r <= radio; r++)
        {
            foreach (var dir in dirs)
            {
                Vector2 candidata = posAurora + dir * r * CELDA;
                if (EsCeldaIndestructible(candidata)) break;
                if (!EsCeldaCaminable(candidata)) continue;

                float dist = Vector2.Distance(transform.position, candidata);
                if (dist < menorDist)
                {
                    menorDist = dist;
                    mejorPos = candidata;
                }
            }
        }
        return mejorPos;
    }

    // ════════════════════════════════════════════════════════
    // DESATASCO
    // ════════════════════════════════════════════════════════

    private IEnumerator Desatascar()
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        for (int i = 0; i < dirs.Length; i++)
        {
            int j = Random.Range(i, dirs.Length);
            (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
        }

        foreach (var dir in dirs)
        {
            Vector2 candidata = SnappedPosition(rigidbody.position) + dir * CELDA;
            if (EsCeldaCaminable(candidata))
            {
                yield return StartCoroutine(MoverATile(candidata));
                yield break;
            }
        }
        yield return new WaitForSeconds(0.5f);
    }

    // ════════════════════════════════════════════════════════
    // BFS
    // ════════════════════════════════════════════════════════

    private Vector2 ObtenerSiguientePasoBFS(Vector2 inicio, Vector2 meta)
    {
        Vector2Int start = Vector2Int.RoundToInt(inicio);
        Vector2Int goal = Vector2Int.RoundToInt(meta);
        if (start == goal) return Vector2.zero;

        Queue<Vector2Int> cola = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> padre = new Dictionary<Vector2Int, Vector2Int>();

        cola.Enqueue(start);
        padre[start] = start;

        Vector2Int[] dirs = {
            new Vector2Int( 0,  1), new Vector2Int( 0, -1),
            new Vector2Int(-1,  0), new Vector2Int( 1,  0)
        };

        bool encontrado = false;
        while (cola.Count > 0)
        {
            Vector2Int actual = cola.Dequeue();
            if (actual == goal) { encontrado = true; break; }

            foreach (var d in dirs)
            {
                Vector2Int vecino = actual + d;
                if (padre.ContainsKey(vecino)) continue;

                Vector2 mundo = new Vector2(vecino.x, vecino.y);
                bool esMeta = vecino == goal;

                if (esMeta || EsCeldaCaminable(mundo))
                {
                    padre[vecino] = actual;
                    cola.Enqueue(vecino);
                }
            }
            if (padre.Count > 300) break;
        }

        if (!encontrado) return Vector2.zero;

        Vector2Int paso = goal;
        while (padre[paso] != start)
            paso = padre[paso];

        return new Vector2(paso.x, paso.y);
    }

    // ════════════════════════════════════════════════════════
    // HUIDA
    // ════════════════════════════════════════════════════════

    private IEnumerator Huir()
    {
        int radio = controlBomba != null ? controlBomba.explosionRadius : 1;

        HashSet<Vector2> zonaPeligro = new HashSet<Vector2>();
        foreach (var bomba in GameObject.FindGameObjectsWithTag("Bomba"))
        {
            foreach (var c in CalcularZonaPeligro(
                SnappedPosition(bomba.transform.position), radio))
                zonaPeligro.Add(c);
        }

        Vector2 mejorPos = Vector2.zero;
        float mejorPuntuacion = -1f;
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        foreach (var dir in dirs)
        {
            Vector2 candidata = SnappedPosition(transform.position) + dir * CELDA;
            if (!EsCeldaCaminable(candidata)) continue;

            bool segura = !zonaPeligro.Contains(candidata);
            float dist = DistanciaABombaCercana(candidata);
            float puntuacion = (segura ? 1000f : 0f) + dist;

            if (puntuacion > mejorPuntuacion)
            {
                mejorPuntuacion = puntuacion;
                mejorPos = candidata;
            }
        }

        if (mejorPos != Vector2.zero)
            yield return StartCoroutine(MoverATile(mejorPos));
    }

    private float DistanciaABombaCercana(Vector2 desde)
    {
        float minDist = float.MaxValue;
        foreach (var bomba in GameObject.FindGameObjectsWithTag("Bomba"))
        {
            float d = Vector2.Distance(desde, bomba.transform.position);
            if (d < minDist) minDist = d;
        }
        return minDist == float.MaxValue ? 999f : minDist;
    }

    private Vector2 EncontrarEscape(Vector2 desdeCelda, int radio)
    {
        HashSet<Vector2> peligro = CalcularZonaPeligro(desdeCelda, radio);
        Queue<Vector2> cola = new Queue<Vector2>();
        HashSet<Vector2> visitadas = new HashSet<Vector2>();

        cola.Enqueue(desdeCelda);
        visitadas.Add(desdeCelda);

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        Vector2 mejorEscape = desdeCelda;
        float mejorDist = 0f;

        while (cola.Count > 0)
        {
            Vector2 actual = cola.Dequeue();

            if (!peligro.Contains(actual) && actual != desdeCelda)
            {
                float dist = Vector2.Distance(desdeCelda, actual);
                if (dist > mejorDist)
                {
                    mejorDist = dist;
                    mejorEscape = actual;
                }
                if (mejorDist >= 3f) break;
            }

            foreach (var d in dirs)
            {
                Vector2 vecino = actual + d * CELDA;
                if (!visitadas.Contains(vecino) && EsCeldaCaminable(vecino))
                {
                    visitadas.Add(vecino);
                    cola.Enqueue(vecino);
                }
            }
        }
        return mejorEscape;
    }

    private HashSet<Vector2> CalcularZonaPeligro(Vector2 centro, int radio)
    {
        var peligro = new HashSet<Vector2>();
        peligro.Add(centro);

        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        foreach (var dir in dirs)
        {
            for (int i = 1; i <= radio; i++)
            {
                Vector2 celda = centro + dir * i * CELDA;
                if (EsCeldaIndestructible(celda)) break;
                peligro.Add(celda);
                if (EsCeldaDestructible(celda)) break;
            }
        }
        return peligro;
    }

    // ════════════════════════════════════════════════════════
    // DETECCIÓN
    // ════════════════════════════════════════════════════════

    private bool HayPeligroCerca()
    {
        Vector2 miPos = SnappedPosition(transform.position);
        int radio = controlBomba != null ? controlBomba.explosionRadius : 1;

        foreach (var bomba in GameObject.FindGameObjectsWithTag("Bomba"))
        {
            var zona = CalcularZonaPeligro(
                SnappedPosition(bomba.transform.position), radio);
            if (zona.Contains(miPos)) return true;
        }

        if (Physics2D.OverlapCircle(transform.position, 0.4f, maskExplosion) != null)
            return true;

        return false;
    }

    private bool AuroraEnLineaDeFuego(Vector2 desdeCelda)
    {
        if (Aurora == null) return false;
        int radio = controlBomba != null ? controlBomba.explosionRadius : 1;
        Vector2 posAurora = SnappedPosition(Aurora.position);

        if (Mathf.Abs(posAurora.y - desdeCelda.y) < 0.1f)
        {
            float dist = Mathf.Abs(posAurora.x - desdeCelda.x);
            if (dist > 0.1f && dist <= radio * CELDA)
            {
                Vector2 dir = posAurora.x > desdeCelda.x ? Vector2.right : Vector2.left;
                if (!HayIndestructibleEntre(desdeCelda, posAurora, dir)) return true;
            }
        }

        if (Mathf.Abs(posAurora.x - desdeCelda.x) < 0.1f)
        {
            float dist = Mathf.Abs(posAurora.y - desdeCelda.y);
            if (dist > 0.1f && dist <= radio * CELDA)
            {
                Vector2 dir = posAurora.y > desdeCelda.y ? Vector2.up : Vector2.down;
                if (!HayIndestructibleEntre(desdeCelda, posAurora, dir)) return true;
            }
        }

        return false;
    }

    private bool HayIndestructibleEntre(Vector2 desde, Vector2 hasta, Vector2 dir)
    {
        Vector2 paso = desde + dir * CELDA;
        int guard = 0;
        while (Vector2.Distance(paso, hasta) > 0.1f && guard < 20)
        {
            guard++;
            if (EsCeldaIndestructible(paso)) return true;
            paso += dir * CELDA;
        }
        return false;
    }

    private bool HayDestructibleHaciaAurora()
    {
        if (Aurora == null) return false;
        Vector2 miPos = SnappedPosition(transform.position);
        Vector2 posAurora = SnappedPosition(Aurora.position);
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        foreach (var dir in dirs)
        {
            Vector2 vecino = miPos + dir * CELDA;
            if (!EsCeldaDestructible(vecino)) continue;
            Vector2 haciaAurora = (posAurora - miPos).normalized;
            if (Vector2.Dot(dir, haciaAurora) > 0.5f) return true;
        }
        return false;
    }

    private GameObject BuscarPowerup(float radio)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radio);
        GameObject mejor = null;
        float menorDist = Mathf.Infinity;

        foreach (var h in hits)
        {
            if (!h.CompareTag("Powerup")) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < menorDist) { menorDist = d; mejor = h.gameObject; }
        }
        return mejor;
    }

    private Vector2 DireccionAleatoria()
    {
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        return dirs[Random.Range(0, dirs.Length)];
    }

    // ════════════════════════════════════════════════════════
    // DETECCIÓN DE CELDAS
    // ════════════════════════════════════════════════════════

    private bool EsCeldaCaminable(Vector2 mundo)
    {
        return !Physics2D.OverlapBox(mundo, Vector2.one * 0.5f, 0f, maskTodo);
    }

    private bool EsCeldaIndestructible(Vector2 mundo)
    {
        return Physics2D.OverlapBox(mundo, Vector2.one * 0.5f, 0f, maskIndestructible);
    }

    private bool EsCeldaDestructible(Vector2 mundo)
    {
        return Physics2D.OverlapBox(mundo, Vector2.one * 0.5f, 0f, maskDestructible);
    }

    private Vector2 SnappedPosition(Vector2 pos)
    {
        return new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
    }

    // ════════════════════════════════════════════════════════
    // MOVIMIENTO CON TIMEOUT
    // ════════════════════════════════════════════════════════

    private IEnumerator MoverATile(Vector2 destino)
    {
        destino = SnappedPosition(destino);
        float tiempoLimite = 1f;
        float tiempoTranscurrido = 0f;

        while (Vector2.Distance(rigidbody.position, destino) > UMBRAL_LLEGADA)
        {
            tiempoTranscurrido += Time.fixedDeltaTime;
            if (tiempoTranscurrido >= tiempoLimite)
            {
                rigidbody.position = SnappedPosition(rigidbody.position);
                ActualizarSprite(Vector2.zero);
                yield break;
            }

            Vector2 diff = destino - rigidbody.position;
            Vector2 dir4 = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y)
                ? (diff.x > 0 ? Vector2.right : Vector2.left)
                : (diff.y > 0 ? Vector2.up : Vector2.down);

            ActualizarSprite(dir4);
            rigidbody.MovePosition(
                rigidbody.position + dir4 * speed * Time.fixedDeltaTime);

            yield return new WaitForFixedUpdate();
        }

        rigidbody.position = destino;
        ActualizarSprite(Vector2.zero);
    }

    // ════════════════════════════════════════════════════════
    // SPRITES
    // ════════════════════════════════════════════════════════

    private void ActualizarSprite(Vector2 dir)
    {
        SpritesAnimadosRender target = activeSpriteRenderer;
        if (dir == Vector2.up) target = spriteRendererUp;
        else if (dir == Vector2.down) target = spriteRendererDown;
        else if (dir == Vector2.left) target = spriteRendererLeft;
        else if (dir == Vector2.right) target = spriteRendererRight;

        spriteRendererUp.enabled = target == spriteRendererUp;
        spriteRendererDown.enabled = target == spriteRendererDown;
        spriteRendererLeft.enabled = target == spriteRendererLeft;
        spriteRendererRight.enabled = target == spriteRendererRight;

        activeSpriteRenderer = target;
        activeSpriteRenderer.idle = dir == Vector2.zero;
    }

    // ════════════════════════════════════════════════════════
    // MUERTE
    // ════════════════════════════════════════════════════════

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isInvincible) return; // ← NUEVO

        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            Vidas--;                          // ← NUEVO
            if (Vidas >= 1)
                DamageSequence();             // ← NUEVO
            else
                DeathSequence();
        }
    }
    // ← NUEVO método completo
    private void DamageSequence()
    {
        isInvincible = true;
        iaActiva = false;
        StopAllCoroutines();
        enabled = false;
        if (controlBomba != null) controlBomba.enabled = false;

        spriteRendererUp.enabled    = false;
        spriteRendererDown.enabled  = false;
        spriteRendererLeft.enabled  = false;
        spriteRendererRight.enabled = false;

        spriteRenderDamage.ResetAnimation();
        spriteRenderDamage.enabled = true;

        Invoke(nameof(OnDamageSequenceEnded), 2f);
    }

    // ← NUEVO método completo
    private void OnDamageSequenceEnded()
    {
        isInvincible = false;
        enabled = true;
        if (controlBomba != null) controlBomba.enabled = true;

        spriteRenderDamage.ResetAnimation();
        spriteRenderDamage.enabled = false;

        spriteRendererDown.enabled = true;
        activeSpriteRenderer       = spriteRendererDown;
        activeSpriteRenderer.idle  = true;

        // Reanudar la IA
        iaActiva = true;
        StartCoroutine(AILoop());
    }

    private void DeathSequence()
    {
        iaActiva = false;
        StopAllCoroutines();
        enabled = false;

        if (controlBomba != null) controlBomba.enabled = false;

        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;
        spriteRenderDeath.enabled = true;

        Invoke(nameof(OnDeathSequenceEnded), 3.25f);
    }

    private void OnDeathSequenceEnded() => gameObject.SetActive(false);
}