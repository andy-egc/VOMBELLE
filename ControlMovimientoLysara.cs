using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════════════════════════
//  LYSARA — IA nivel "jugador humano experto" — Vombelle
//
//  Qué hace que parezca un jugador real:
//
//  1. LECTURA DE TABLERO EN TIEMPO REAL
//     Cada tick evalúa: cuántas salidas tiene, si está acorralada,
//     cuánto tiempo le queda antes de que explote una bomba cercana.
//
//  2. PRESIÓN ACTIVA — no espera que Aurora venga
//     Si Aurora está en una zona abierta la acorrala bloqueando salidas.
//     Si hay un pasillo estrecho la empuja hacia un callejón antes de bombar.
//
//  3. ATAQUE INMEDIATO al tener línea de fuego
//     En cuanto Aurora queda en la misma fila/columna sin muro entre ellas,
//     coloca bomba y se va — sin esperar al bucle de 0.22 s.
//
//  4. FUGA TÁCTICA con prioridad de salidas
//     Al huir no elige la celda más lejana sino la que tenga MÁS salidas
//     disponibles (= más libertad de movimiento = menor riesgo de trampa).
//
//  5. TRAMPA ACTIVA
//     Si Aurora está en un pasillo con pocas salidas, Lysara intenta
//     posicionarse en esa salida para forzar la explosión.
//
//  6. MEMORIA DE CALOR
//     Recuerda las celdas donde explotó una bomba recientemente
//     y las evita aunque ya no haya collider activo.
//
//  7. TIMING HUMANO — reacción variable
//     Pequeña variación aleatoria en timeBetweenMoves para que no parezca
//     una máquina perfectamente sincronizada.
// ════════════════════════════════════════════════════════════════════════════

public class ControlMovimientoLysara : MonoBehaviour
{
    // ── Componentes ──────────────────────────────────────────────────────────
    public new Rigidbody2D rigidbody { get; private set; }
    public float speed = 5f;

    // ── Sprites ──────────────────────────────────────────────────────────────
    public SpritesAnimadosRender spriteRendererUp;
    public SpritesAnimadosRender spriteRendererDown;
    public SpritesAnimadosRender spriteRendererLeft;
    public SpritesAnimadosRender spriteRendererRight;
    public SpritesAnimadosRender spriteRenderDeath;
    public SpritesAnimadosRender spriteRenderDamage;
    private SpritesAnimadosRender activeSpriteRenderer;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("IA — General")]
    public Transform Aurora;
    public float delayInicio = 3f;
    public float timeBetweenMoves = 0.22f;

    [Header("IA — Combate")]
    [Range(0f, 1f)] public float bombChance = 0.92f;
    [Range(0f, 1f)] public float wanderChance = 0.15f;
    [Range(0f, 0.12f)] public float timingJitter = 0.06f;

    [Header("Vidas")]
    public int Vidas = 3;

    // ── Estado visible en inspector ───────────────────────────────────────────
    public enum EstadoIA { Idle, Perseguir, Acorralar, Huir, Bomba, Powerup, Recuperando }
    public EstadoIA estado = EstadoIA.Idle;

    // ── Privados ─────────────────────────────────────────────────────────────
    private ControlBomba controlBomba;
    private bool iaActiva = false;
    private bool isInvincible = false;
    private bool enRecuperacion = false;
    private bool ataqueInmediato = false;

    private Vector2 posicionAnterior;
    private float tiempoAtasco = 0f;
    private const float LIM_ATASCO = 0.9f;
    private const float CELDA = 1f;
    private const float UMBRAL = 0.05f;
    private const int BFS_LIMITE = 400;

    private readonly Queue<Vector2> memoriaCeldas = new Queue<Vector2>();
    private const int MEMORIA_SIZE = 12;

    private readonly Dictionary<Vector2, float> memoriaCalor = new Dictionary<Vector2, float>();
    private const float CALOR_DURACION = 1.2f;

    private LayerMask maskExp;
    private LayerMask maskInd;
    private LayerMask maskDes;
    private LayerMask maskTodo;

    private int capaExp = -1;
    private int CapaExp
    {
        get { if (capaExp < 0) capaExp = LayerMask.NameToLayer("Explosion"); return capaExp; }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // INICIO
    // ═════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        controlBomba = GetComponent<ControlBomba>();
        activeSpriteRenderer = spriteRendererDown;
        maskExp = LayerMask.GetMask("Explosion");
        maskInd = LayerMask.GetMask("TilesIndestructibles");
        maskDes = LayerMask.GetMask("TilesDestructibles");
        maskTodo = maskInd | maskDes;
    }

    private void Start() => StartCoroutine(Iniciar());

    private IEnumerator Iniciar()
    {
        rigidbody.position = Snap(rigidbody.position);
        yield return new WaitForSeconds(delayInicio);
        posicionAnterior = rigidbody.position;
        iaActiva = true;
        _corBuclePrincipal = StartCoroutine(BuclePrincipal());
        StartCoroutine(BucleVigilancia());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUCLE DE VIGILANCIA — detecta línea de fuego y ataca inmediatamente
    // ═════════════════════════════════════════════════════════════════════════

    // ── Referencia a la coroutine del bucle principal para poder pararla correctamente ──
    private Coroutine _corBuclePrincipal;

    // Verdadero mientras hay una bomba colocada por Lysara aún activa en el mapa.
    // CLAVE: impide colocar segunda bomba antes de que explote la primera.
    private bool bombaPuesta = false;

    private IEnumerator BucleVigilancia()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.08f);

            // No ataca si: IA inactiva, recuperando, ya atacando, o YA HAY BOMBA ACTIVA
            if (!iaActiva || enRecuperacion || ataqueInmediato || bombaPuesta) continue;

            Vector2 yo = Snap(transform.position);
            if (HayPeligro(yo)) continue;
            if (controlBomba == null || !controlBomba.enabled) continue;

            if (AuroraEnLinea(yo))
            {
                // Calcular escape considerando TODAS las bombas activas, no solo la nueva
                Vector2 esc = MejorEscapeTotal(yo, controlBomba.explosionRadius);
                if (esc != yo && Vector2.Distance(yo, esc) >= 2f)
                {
                    ataqueInmediato = true;
                    // Parar BuclePrincipal por referencia (no por string — el string falla en Unity)
                    if (_corBuclePrincipal != null) StopCoroutine(_corBuclePrincipal);
                    StartCoroutine(AtaqueInmediato(yo, esc));
                }
            }
        }
    }

    private IEnumerator AtaqueInmediato(Vector2 yo, Vector2 escape)
    {
        estado = EstadoIA.Bomba;
        bombaPuesta = true;
        controlBomba.TryPlaceBomb();
        yield return new WaitForSeconds(0.03f);
        yield return StartCoroutine(SeguirRuta(RutaBFS(Snap(transform.position), escape)));
        yield return StartCoroutine(EsperarZonaLimpia());
        bombaPuesta = false;
        ataqueInmediato = false;
        _corBuclePrincipal = StartCoroutine(BuclePrincipal());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUCLE PRINCIPAL
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator BuclePrincipal()
    {
        while (true)
        {
            float espera = timeBetweenMoves + Random.Range(-timingJitter, timingJitter);
            yield return new WaitForSeconds(Mathf.Max(0.08f, espera));
            if (!iaActiva) yield break;

            LimpiarCalor();
            Vector2 yo = Snap(transform.position);

            // ── 0. Anti-atasco ────────────────────────────────────────────
            if (Vector2.Distance(rigidbody.position, posicionAnterior) < 0.08f)
            {
                tiempoAtasco += timeBetweenMoves;
                if (tiempoAtasco >= LIM_ATASCO)
                {
                    tiempoAtasco = 0f;
                    yield return StartCoroutine(Desatascar(yo));
                    posicionAnterior = rigidbody.position;
                    continue;
                }
            }
            else tiempoAtasco = 0f;
            posicionAnterior = rigidbody.position;

            // ── 1. PELIGRO — huida táctica ────────────────────────────────
            if (HayPeligro(yo))
            {
                estado = EstadoIA.Huir;
                yield return StartCoroutine(HuirTactico(yo));
                yield return StartCoroutine(EsperarZonaLimpia());
                continue;
            }

            // ── 2. BOMBA con lógica avanzada ──────────────────────────────
            if (!enRecuperacion && controlBomba != null && controlBomba.enabled)
            {
                DecisionBomba decision = EvaluarDecisionBomba(yo);

                if (!bombaPuesta && decision.debeColocar && Random.value < bombChance)
                {
                    // Escape que considera la nueva bomba + las ya existentes en el mapa
                    Vector2 esc = MejorEscapeTotal(yo, controlBomba.explosionRadius);
                    if (esc != yo && Vector2.Distance(yo, esc) >= 2f)
                    {
                        estado = EstadoIA.Bomba;
                        bombaPuesta = true;
                        controlBomba.TryPlaceBomb();
                        yield return new WaitForSeconds(0.03f);
                        yield return StartCoroutine(SeguirRuta(RutaBFS(Snap(transform.position), esc)));
                        yield return StartCoroutine(EsperarZonaLimpia());
                        bombaPuesta = false;
                        continue;
                    }
                }

                if (decision.debeAcorralar)
                {
                    estado = EstadoIA.Acorralar;
                    Vector2 sig = PrimerPaso(yo, decision.celdaBloqueo);
                    if (sig != Vector2.zero && !CeldaEnPeligro(sig))
                    { yield return StartCoroutine(MoverATile(sig)); continue; }
                }
            }

            // ── 3. POWERUP ────────────────────────────────────────────────
            GameObject pup = PowerupSeguro(7f);
            if (pup != null)
            {
                estado = EstadoIA.Powerup;
                Vector2 sig = PrimerPaso(yo, Snap(pup.transform.position));
                if (sig != Vector2.zero)
                { yield return StartCoroutine(MoverATile(sig)); continue; }
            }

            // ── 4. PERSEGUIR con presión ──────────────────────────────────
            if (Aurora != null)
            {
                estado = EstadoIA.Perseguir;
                Vector2 objetivo = ObjetivoPresion(yo);
                Vector2 siguiente = PrimerPaso(yo, objetivo);

                if (siguiente != Vector2.zero && Random.value < wanderChance)
                {
                    Vector2 alt = CeldaAlternativa(yo, siguiente);
                    if (alt != Vector2.zero) siguiente = alt;
                }

                if (siguiente != Vector2.zero)
                { yield return StartCoroutine(MoverATile(siguiente)); continue; }
            }

            // ── 5. DEAMBULAR con memoria ──────────────────────────────────
            Vector2 libre = CeldaLibreConMemoria(yo);
            if (libre != Vector2.zero)
                yield return StartCoroutine(MoverATile(libre));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DECISIÓN DE BOMBA AVANZADA
    // ═════════════════════════════════════════════════════════════════════════

    private struct DecisionBomba
    {
        public bool debeColocar;
        public bool debeAcorralar;
        public Vector2 celdaBloqueo;
    }

    private DecisionBomba EvaluarDecisionBomba(Vector2 yo)
    {
        DecisionBomba d = new DecisionBomba();

        int r = controlBomba != null ? controlBomba.explosionRadius : 2;

        // ── Razón 1: Aurora en línea de fuego directa ─────────────────────
        if (AuroraEnLinea(yo)) { d.debeColocar = true; return d; }

        if (Aurora != null)
        {
            Vector2 posA = Snap(Aurora.position);
            float dist = ManhattanDist(yo, posA);

            // ── Razón 2: Aurora muy cerca (distancia Manhattan) ────────────
            if (dist <= r + 0.6f) { d.debeColocar = true; return d; }

            // ── Razón 3: Bloque destructible que, al volar, expone a Aurora ─
            if (DestructibleQueExpondriaAurora(yo)) { d.debeColocar = true; return d; }

            // ── Razón 4: Hay destructible en dirección hacia Aurora ────────
            // (abrir camino para poder perseguirla / acorralarla)
            if (DestructibleHaciaAurora(yo)) { d.debeColocar = true; return d; }

            // ── Razón 5: Acorralamiento ────────────────────────────────────
            int salidasAurora = ContarSalidas(posA);
            if (salidasAurora <= 2 && dist <= 6f)
            {
                Vector2 bloqueo = EncontrarBloqueoOptimo(yo, posA);
                if (bloqueo != yo)
                {
                    d.debeAcorralar = true;
                    d.celdaBloqueo = bloqueo;
                    if (Vector2.Distance(yo, bloqueo) < 0.1f)
                    {
                        d.debeColocar = true;
                        d.debeAcorralar = false;
                    }
                }
            }
        }

        // ── Razón 6: Cualquier destructible adyacente (explorar/abrir mapa) ─
        // Solo si no hay nada más urgente y llevamos tiempo sin atacar.
        // Usa bombChance reducida para no ser compulsiva con esto.
        if (!d.debeColocar && !d.debeAcorralar && HayDestructibleAdyacente(yo))
            d.debeColocar = Random.value < 0.35f; // bomba de oportunidad, menos frecuente

        return d;
    }

    // ¿Hay algún bloque destructible en las 4 celdas adyacentes?
    private bool HayDestructibleAdyacente(Vector2 yo)
    {
        foreach (Vector2 dir in Dirs())
            if (EsDestructible(yo + dir)) return true;
        return false;
    }

    private Vector2 EncontrarBloqueoOptimo(Vector2 yo, Vector2 posAurora)
    {
        Vector2 mejor = yo;
        float menorD = Mathf.Infinity;
        int r = controlBomba != null ? controlBomba.explosionRadius : 2;

        foreach (Vector2 dir in Dirs())
        {
            for (int i = 1; i <= r + 2; i++)
            {
                Vector2 c = posAurora + dir * i;
                if (EsIndestructible(c)) break;
                if (!EsCaminable(c)) continue;

                if (AuroraEnLineaDesde(c, posAurora))
                {
                    Vector2 esc = MejorEscape(c, r);
                    if (esc == c) continue;
                    float dist = Vector2.Distance(yo, c);
                    if (dist < menorD) { menorD = dist; mejor = c; }
                }
            }
        }
        return mejor;
    }

    private bool AuroraEnLineaDesde(Vector2 desde, Vector2 posAurora)
    {
        int r = controlBomba != null ? controlBomba.explosionRadius : 2;

        if (Mathf.Abs(posAurora.y - desde.y) < 0.1f)
        {
            float dist = Mathf.Abs(posAurora.x - desde.x);
            if (dist > 0.1f && dist <= r)
                if (!MuroEntre(desde, posAurora, posAurora.x > desde.x ? Vector2.right : Vector2.left))
                    return true;
        }
        if (Mathf.Abs(posAurora.x - desde.x) < 0.1f)
        {
            float dist = Mathf.Abs(posAurora.y - desde.y);
            if (dist > 0.1f && dist <= r)
                if (!MuroEntre(desde, posAurora, posAurora.y > desde.y ? Vector2.up : Vector2.down))
                    return true;
        }
        return false;
    }

    private bool DestructibleQueExpondriaAurora(Vector2 yo)
    {
        if (Aurora == null) return false;
        int r = controlBomba != null ? controlBomba.explosionRadius : 2;

        foreach (Vector2 dir in Dirs())
        {
            for (int i = 1; i <= r; i++)
            {
                Vector2 c = yo + dir * i;
                if (EsIndestructible(c)) break;
                if (!EsDestructible(c)) continue;
                Vector2 detras = c + dir;
                if (Snap(Aurora.position) == detras) return true;
                break;
            }
        }
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HUIDA TÁCTICA
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator HuirTactico(Vector2 desde)
    {
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        HashSet<Vector2> peligro = ZonasActivasDePeligro(radio);
        Vector2 destino = CeldaMasSeguraTactica(desde, peligro);
        if (destino == desde) yield break;
        List<Vector2> ruta = RutaBFSEscape(desde, destino);
        yield return StartCoroutine(SeguirRutaRapida(ruta));
    }

    private IEnumerator SeguirRutaRapida(List<Vector2> ruta)
    {
        foreach (Vector2 paso in ruta)
        {
            if (!HayPeligro(Snap(transform.position))) yield break;
            yield return StartCoroutine(MoverATile(paso));
        }
    }

    private IEnumerator SeguirRuta(List<Vector2> ruta)
    {
        foreach (Vector2 paso in ruta)
        {
            if (!HayPeligro(Snap(transform.position))) yield break;
            yield return StartCoroutine(MoverATile(paso));
        }
    }

    private IEnumerator EsperarZonaLimpia()
    {
        float tMax = controlBomba != null
            ? controlBomba.bombFuseTime + controlBomba.explosionDuration + 1.0f
            : 5f;
        float t = 0f;
        while (t < tMax)
        {
            if (!HayPeligro(Snap(transform.position))) yield break;
            yield return StartCoroutine(HuirTactico(Snap(transform.position)));
            t += timeBetweenMoves + 0.05f;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private Vector2 CeldaMasSeguraTactica(Vector2 desde, HashSet<Vector2> peligro)
    {
        Queue<Vector2> cola = new Queue<Vector2>();
        HashSet<Vector2> visitadas = new HashSet<Vector2>();
        cola.Enqueue(desde); visitadas.Add(desde);

        Vector2 mejorCelda = desde;
        float mejorScore = float.MinValue;

        while (cola.Count > 0 && visitadas.Count < BFS_LIMITE)
        {
            Vector2 actual = cola.Dequeue();

            float score = 0f;
            if (!peligro.Contains(actual)) score += 200f;
            if (!CeldaEnCalor(actual)) score += 50f;
            score += ContarSalidas(actual) * 30f;
            score += DistBomba(actual) * 6f;
            if (memoriaCeldas.Contains(actual)) score -= 20f;
            score += Random.Range(-8f, 8f);

            if (score > mejorScore) { mejorScore = score; mejorCelda = actual; }

            foreach (Vector2 d in Dirs())
            {
                Vector2 v = actual + d;
                if (!visitadas.Contains(v) && EsCaminable(v))
                { visitadas.Add(v); cola.Enqueue(v); }
            }
        }
        return mejorCelda;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // OBJETIVO DE PERSECUCIÓN CON PRESIÓN
    // ═════════════════════════════════════════════════════════════════════════

    private Vector2 ObjetivoPresion(Vector2 yo)
    {
        if (Aurora == null) return yo;
        Vector2 posA = Snap(Aurora.position);
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;

        Vector2 mejor = posA;
        float mejorScore = float.MinValue;

        foreach (Vector2 dir in Dirs())
        {
            for (int r = 1; r <= radio + 1; r++)
            {
                Vector2 c = posA + dir * r;
                if (EsIndestructible(c)) break;
                if (!EsCaminable(c) || HayPeligro(c) || CeldaEnCalor(c)) continue;

                float score = 0f;
                if (AuroraEnLineaDesde(c, posA)) score += 100f;
                score -= Vector2.Distance(yo, c) * 5f;
                score -= ContarSalidas(posA) * 8f;
                score += (4 - ContarSalidas(c)) * 10f;

                if (score > mejorScore) { mejorScore = score; mejor = c; }
            }
        }
        return mejor;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BFS
    // ═════════════════════════════════════════════════════════════════════════

    private Vector2 PrimerPaso(Vector2 inicio, Vector2 meta)
    {
        var ruta = RutaBFS(inicio, meta);
        return ruta.Count > 0 ? ruta[0] : Vector2.zero;
    }

    private List<Vector2> RutaBFS(Vector2 inicio, Vector2 meta)
    {
        Vector2Int s = VI(inicio), g = VI(meta);
        var result = new List<Vector2>();
        if (s == g) return result;

        var cola = new Queue<Vector2Int>();
        var padre = new Dictionary<Vector2Int, Vector2Int>();
        cola.Enqueue(s); padre[s] = s;

        Vector2Int[] dirs = { new Vector2Int(0,1), new Vector2Int(0,-1),
                               new Vector2Int(-1,0), new Vector2Int(1,0) };
        bool ok = false;
        while (cola.Count > 0 && padre.Count < BFS_LIMITE)
        {
            Vector2Int c = cola.Dequeue();
            if (c == g) { ok = true; break; }
            foreach (var d in dirs)
            {
                Vector2Int v = c + d;
                if (padre.ContainsKey(v)) continue;
                Vector2 w = new Vector2(v.x, v.y);
                bool segura = v == g || (EsCaminable(w) && !HayPeligro(w) && !CeldaEnCalor(w));
                if (segura) { padre[v] = c; cola.Enqueue(v); }
            }
        }
        if (!ok) return result;

        var pila = new List<Vector2>();
        Vector2Int paso = g;
        while (paso != s) { pila.Add(new Vector2(paso.x, paso.y)); paso = padre[paso]; }
        pila.Reverse();
        return pila;
    }

    private List<Vector2> RutaBFSEscape(Vector2 inicio, Vector2 meta)
    {
        Vector2Int s = VI(inicio), g = VI(meta);
        var result = new List<Vector2>();
        if (s == g) return result;

        var cola = new Queue<Vector2Int>();
        var padre = new Dictionary<Vector2Int, Vector2Int>();
        cola.Enqueue(s); padre[s] = s;

        Vector2Int[] dirs = { new Vector2Int(0,1), new Vector2Int(0,-1),
                               new Vector2Int(-1,0), new Vector2Int(1,0) };
        bool ok = false;
        while (cola.Count > 0 && padre.Count < BFS_LIMITE)
        {
            Vector2Int c = cola.Dequeue();
            if (c == g) { ok = true; break; }
            foreach (var d in dirs)
            {
                Vector2Int v = c + d;
                if (padre.ContainsKey(v)) continue;
                Vector2 w = new Vector2(v.x, v.y);
                bool libre = v == g || (!EsIndestructible(w) && !EsDestructible(w));
                if (libre) { padre[v] = c; cola.Enqueue(v); }
            }
        }
        if (!ok) return result;

        var pila = new List<Vector2>();
        Vector2Int paso = g;
        while (paso != s) { pila.Add(new Vector2(paso.x, paso.y)); paso = padre[paso]; }
        pila.Reverse();
        return pila;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ESCAPE PARA BOMBA
    // ═════════════════════════════════════════════════════════════════════════

    // MejorEscapeTotal: calcula zona peligrosa combinando
    //   · La bomba que Lysara VA A COLOCAR (en 'desde')
    //   · Todas las bombas YA activas en el mapa
    // Así nunca huye hacia una zona que ya está en llamas.
    private Vector2 MejorEscapeTotal(Vector2 desde, int radio)
    {
        // Zona de la bomba que vamos a colocar
        HashSet<Vector2> peligro = ZonaBomba(desde, radio);

        // Añadir zonas de todas las bombas ya existentes
        foreach (var bomba in GameObject.FindGameObjectsWithTag("Bomba"))
        {
            Vector2 posBomba = Snap(bomba.transform.position);
            foreach (var c in ZonaBomba(posBomba, radio))
                peligro.Add(c);
        }

        Queue<Vector2> cola = new Queue<Vector2>();
        HashSet<Vector2> visit = new HashSet<Vector2>();
        cola.Enqueue(desde); visit.Add(desde);

        Vector2 mejor = desde;
        float mejorScore = float.MinValue;

        while (cola.Count > 0 && visit.Count < 300)
        {
            Vector2 a = cola.Dequeue();
            if (!peligro.Contains(a) && a != desde)
            {
                float score = 0f;
                score += Vector2.Distance(desde, a) * 10f;  // lejos de la bomba
                score += ContarSalidas(a) * 20f;             // muchas salidas = más libertad
                if (CeldaEnCalor(a)) score -= 60f;           // evitar zona de explosión reciente
                if (peligro.Contains(a)) score -= 999f;      // nunca elegir celda peligrosa
                score += Random.Range(-4f, 4f);              // ruido humano

                if (score > mejorScore) { mejorScore = score; mejor = a; }
            }
            foreach (Vector2 d in Dirs())
            {
                Vector2 v = a + d;
                // Solo explorar celdas que no sean muros (puede pasar por peligro en emergencia)
                if (!visit.Contains(v) && EsCaminable(v)) { visit.Add(v); cola.Enqueue(v); }
            }
        }
        return mejor;
    }

    // Versión legacy mantenida por compatibilidad interna (huida táctica)
    private Vector2 MejorEscape(Vector2 desde, int radio) => MejorEscapeTotal(desde, radio);

    // ═════════════════════════════════════════════════════════════════════════
    // DETECCIÓN DE PELIGRO
    // ═════════════════════════════════════════════════════════════════════════

    private bool HayPeligro(Vector2 celda)
    {
        if (Physics2D.OverlapCircle(transform.position, 0.42f, maskExp) != null) return true;
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        foreach (var bomba in GameObject.FindGameObjectsWithTag("Bomba"))
            if (ZonaBomba(Snap(bomba.transform.position), radio).Contains(celda)) return true;
        return false;
    }

    private bool CeldaEnPeligro(Vector2 celda)
    {
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        foreach (var bomba in GameObject.FindGameObjectsWithTag("Bomba"))
            if (ZonaBomba(Snap(bomba.transform.position), radio).Contains(celda)) return true;
        return false;
    }

    private HashSet<Vector2> ZonasActivasDePeligro(int radio)
    {
        var z = new HashSet<Vector2>();
        foreach (var b in GameObject.FindGameObjectsWithTag("Bomba"))
            foreach (var c in ZonaBomba(Snap(b.transform.position), radio)) z.Add(c);
        return z;
    }

    private HashSet<Vector2> ZonaBomba(Vector2 centro, int radio)
    {
        var z = new HashSet<Vector2>();
        z.Add(centro);
        foreach (Vector2 dir in Dirs())
            for (int i = 1; i <= radio; i++)
            {
                Vector2 c = centro + dir * i;
                if (EsIndestructible(c)) break;
                z.Add(c);
                if (EsDestructible(c)) break;
            }
        return z;
    }

    private float DistBomba(Vector2 desde)
    {
        float min = float.MaxValue;
        foreach (var b in GameObject.FindGameObjectsWithTag("Bomba"))
        {
            float d = Vector2.Distance(desde, b.transform.position);
            if (d < min) min = d;
        }
        return min == float.MaxValue ? 999f : min;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MEMORIA DE CALOR
    // ═════════════════════════════════════════════════════════════════════════

    private void RegistrarCalor(Vector2 centro)
    {
        int r = controlBomba != null ? controlBomba.explosionRadius : 2;
        foreach (Vector2 dir in Dirs())
            for (int i = 0; i <= r; i++)
            {
                Vector2 c = centro + dir * i;
                if (EsIndestructible(c)) break;
                memoriaCalor[c] = Time.time + CALOR_DURACION;
            }
    }

    private bool CeldaEnCalor(Vector2 c) =>
        memoriaCalor.TryGetValue(c, out float t) && Time.time < t;

    private void LimpiarCalor()
    {
        var exp = new List<Vector2>();
        foreach (var kv in memoriaCalor) if (Time.time >= kv.Value) exp.Add(kv.Key);
        foreach (var k in exp) memoriaCalor.Remove(k);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ANÁLISIS DEL TABLERO
    // ═════════════════════════════════════════════════════════════════════════

    private int ContarSalidas(Vector2 celda)
    {
        int n = 0;
        foreach (Vector2 d in Dirs())
            if (EsCaminable(celda + d) && !HayPeligro(celda + d)) n++;
        return n;
    }

    private float ManhattanDist(Vector2 a, Vector2 b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // ═════════════════════════════════════════════════════════════════════════
    // DETECCIÓN DE AURORA
    // ═════════════════════════════════════════════════════════════════════════

    private bool AuroraEnLinea(Vector2 yo)
    {
        if (Aurora == null) return false;
        return AuroraEnLineaDesde(yo, Snap(Aurora.position));
    }

    private bool MuroEntre(Vector2 desde, Vector2 hasta, Vector2 dir)
    {
        Vector2 p = desde + dir;
        int g = 0;
        while (Vector2.Distance(p, hasta) > 0.1f && g++ < 20)
        {
            if (EsIndestructible(p)) return true;
            p += dir;
        }
        return false;
    }

    private bool DestructibleHaciaAurora(Vector2 yo)
    {
        if (Aurora == null) return false;
        Vector2 a = Snap(Aurora.position);
        foreach (Vector2 dir in Dirs())
        {
            Vector2 v = yo + dir;
            if (!EsDestructible(v)) continue;
            if (Vector2.Dot(dir, (a - yo).normalized) > 0.5f) return true;
        }
        return false;
    }

    private GameObject PowerupSeguro(float radio)
    {
        GameObject mejor = null;
        float min = Mathf.Infinity;
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, radio))
        {
            if (!h.CompareTag("Powerup")) continue;
            Vector2 cp = Snap(h.transform.position);
            if (HayPeligro(cp) || CeldaEnCalor(cp)) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < min) { min = d; mejor = h.gameObject; }
        }
        return mejor;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // VARIABILIDAD
    // ═════════════════════════════════════════════════════════════════════════

    private Vector2 CeldaAlternativa(Vector2 desde, Vector2 excluir)
    {
        foreach (Vector2 d in DirsAleatorio())
        {
            Vector2 c = desde + d;
            if (c == excluir) continue;
            if (EsCaminable(c) && !HayPeligro(c) && !CeldaEnCalor(c)) return c;
        }
        return Vector2.zero;
    }

    private Vector2 CeldaLibreConMemoria(Vector2 desde)
    {
        Vector2 mejorCelda = Vector2.zero;
        float mejorScore = float.MinValue;

        foreach (Vector2 d in DirsAleatorio())
        {
            Vector2 c = desde + d;
            if (!EsCaminable(c) || HayPeligro(c) || CeldaEnCalor(c)) continue;

            float score = 0f;
            if (memoriaCeldas.Contains(c)) score -= 30f;
            if (EsDestructible(c)) score += 8f;
            score += ContarSalidas(c) * 5f;
            score += Random.Range(-4f, 4f);

            if (score > mejorScore) { mejorScore = score; mejorCelda = c; }
        }
        return mejorCelda;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DESATASCAR
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator Desatascar(Vector2 desde)
    {
        foreach (Vector2 d in DirsAleatorio())
        {
            Vector2 c = desde + d;
            if (EsCaminable(c) && !HayPeligro(c) && !CeldaEnCalor(c))
            { yield return StartCoroutine(MoverATile(c)); yield break; }
        }
        foreach (Vector2 d in DirsAleatorio())
        {
            Vector2 c = desde + d;
            if (EsCaminable(c))
            { yield return StartCoroutine(MoverATile(c)); yield break; }
        }
        yield return new WaitForSeconds(0.15f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MOVIMIENTO
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator MoverATile(Vector2 dest)
    {
        dest = Snap(dest);
        float t = 0f;

        while (Vector2.Distance(rigidbody.position, dest) > UMBRAL)
        {
            t += Time.fixedDeltaTime;
            if (t >= 1.1f)
            { rigidbody.position = Snap(rigidbody.position); SetSprite(Vector2.zero); yield break; }

            Vector2 diff = dest - rigidbody.position;
            Vector2 dir = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y)
                ? (diff.x > 0 ? Vector2.right : Vector2.left)
                : (diff.y > 0 ? Vector2.up : Vector2.down);

            SetSprite(dir);
            rigidbody.MovePosition(rigidbody.position + dir * speed * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        RegistrarMemoria(dest);
        rigidbody.position = dest;
        SetSprite(Vector2.zero);
    }

    private void RegistrarMemoria(Vector2 celda)
    {
        memoriaCeldas.Enqueue(celda);
        if (memoriaCeldas.Count > MEMORIA_SIZE) memoriaCeldas.Dequeue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SPRITES
    // ═════════════════════════════════════════════════════════════════════════

    private void SetSprite(Vector2 dir)
    {
        SpritesAnimadosRender t =
            dir == Vector2.up ? spriteRendererUp :
            dir == Vector2.down ? spriteRendererDown :
            dir == Vector2.left ? spriteRendererLeft :
            dir == Vector2.right ? spriteRendererRight :
            activeSpriteRenderer;

        spriteRendererUp.enabled = t == spriteRendererUp;
        spriteRendererDown.enabled = t == spriteRendererDown;
        spriteRendererLeft.enabled = t == spriteRendererLeft;
        spriteRendererRight.enabled = t == spriteRendererRight;
        activeSpriteRenderer = t;
        activeSpriteRenderer.idle = dir == Vector2.zero;
    }

    private void OcultarMovimiento()
    {
        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DAÑO Y MUERTE
    // ═════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter2D(Collider2D other) => ProcesarExplosion(other);
    private void OnTriggerStay2D(Collider2D other) => ProcesarExplosion(other);

    private void ProcesarExplosion(Collider2D other)
    {
        if (isInvincible) return;
        if (other.gameObject.layer != CapaExp) return;

        RegistrarCalor(Snap(other.transform.position));

        Vidas--;
        if (Vidas >= 1) IniciarDano();
        else IniciarMuerte();
    }

    private void IniciarDano()
    {
        isInvincible = true;
        enRecuperacion = true;
        ataqueInmediato = false;
        iaActiva = false;

        StopAllCoroutines();
        speed = 5f;
        if (controlBomba != null) controlBomba.enabled = false;

        OcultarMovimiento();
        spriteRenderDamage.ResetAnimation();
        spriteRenderDamage.enabled = true;

        Invoke(nameof(TerminarDano), 2f);
    }

    private void TerminarDano()
    {
        spriteRenderDamage.ResetAnimation();
        spriteRenderDamage.enabled = false;
        spriteRendererDown.enabled = true;
        activeSpriteRenderer = spriteRendererDown;
        activeSpriteRenderer.idle = true;

        if (controlBomba != null)
        {
            controlBomba.enabled = true;
            controlBomba.ResetPowerUps(); // Después de OnEnable(), forzamos los valores correctos
        }
        isInvincible = false;
        enRecuperacion = false;
        ataqueInmediato = false;

        iaActiva = true;
        bombaPuesta = false;
        _corBuclePrincipal = StartCoroutine(BuclePrincipal());
        StartCoroutine(BucleVigilancia());
    }

    private void IniciarMuerte()
    {
        isInvincible = true;
        iaActiva = false;

        StopAllCoroutines();
        CancelInvoke();
        if (controlBomba != null) controlBomba.enabled = false;

        OcultarMovimiento();
        spriteRenderDamage.enabled = false;
        spriteRenderDeath.enabled = true;

        Invoke(nameof(FinMuerte), 3.25f);
    }

    private void FinMuerte() => gameObject.SetActive(false);

    // ═════════════════════════════════════════════════════════════════════════
    // UTILIDADES
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Vector2[] _dirs =
        { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    private Vector2[] Dirs() => _dirs;

    private Vector2[] DirsAleatorio()
    {
        Vector2[] d = (Vector2[])_dirs.Clone();
        for (int i = 0; i < d.Length; i++)
        { int j = Random.Range(i, d.Length); (d[i], d[j]) = (d[j], d[i]); }
        return d;
    }

    private bool EsCaminable(Vector2 p) => !Physics2D.OverlapBox(p, Vector2.one * 0.5f, 0f, maskTodo);
    private bool EsIndestructible(Vector2 p) => Physics2D.OverlapBox(p, Vector2.one * 0.5f, 0f, maskInd);
    private bool EsDestructible(Vector2 p) => Physics2D.OverlapBox(p, Vector2.one * 0.5f, 0f, maskDes);

    private Vector2 Snap(Vector2 p) => new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
    private Vector2Int VI(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
}