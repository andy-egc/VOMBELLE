using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════════════════════════
//  LYSARA — IA v3.1 — DEFENSIVA + ESTABLE — Vombelle
//
//  CAMBIOS v3.1:
//   · ELIMINADA la recursión mutua HuirTactico ↔ SeguirRuta (stack overflow).
//     Ahora SeguirRuta solo aborta; el BuclePrincipal decide el siguiente paso.
//   · BFS de escape filtra activamente celdas peligrosas durante la búsqueda.
//   · Contador de profundidad como red de seguridad.
//   · Inspector reorganizado: todos los parámetros expuestos, agrupados y
//     con tooltips explicativos.
//
//  FILOSOFÍA: "Un daño evitado vale más que un golpe arriesgado."
//   Prioridad 1: SUPERVIVENCIA   Prioridad 2: ATAQUE
// ════════════════════════════════════════════════════════════════════════════

public class ControlMovimientoLysara : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR — CONFIGURACIÓN ACCESIBLE
    // ─────────────────────────────────────────────────────────────────────────

    [Header("═══ REFERENCIAS ═══")]
    [Tooltip("Transform de la jugadora (Aurora). Arrástralo aquí desde la escena.")]
    public Transform Aurora;

    [Header("═══ NIVEL DE INTELIGENCIA ═══")]
    [Tooltip("Preset general de dificultad. Si aplicarPresetAutomatico = true, ajusta muchos parámetros de golpe. Ponlo en PERSONALIZADO para controlar todo manualmente.")]
    public NivelInteligencia nivelInteligencia = NivelInteligencia.Normal;

    [Tooltip("Si está marcado, al iniciar se aplican los valores del preset elegido. Desmárcalo si quieres configurar todo a mano.")]
    public bool aplicarPresetAutomatico = true;

    [Header("═══ CAPACIDADES (toggles individuales) ═══")]
    [Tooltip("Si está desactivado, Lysara NO coloca bombas. Úsalo para modo puramente evasivo / tutorial.")]
    public bool puedeColocarBombas = true;

    [Tooltip("Si está desactivado, Lysara no anticipa los movimientos de Aurora. La hace menos letal.")]
    public bool usarPrediccionAurora = true;

    [Tooltip("Si está desactivado, Lysara no intentará acorralar a Aurora en pasillos.")]
    public bool puedeAcorralar = true;

    [Tooltip("Si está desactivado, Lysara no rompe bloques destructibles para abrir camino.")]
    public bool puedeRomperBloques = true;

    [Tooltip("Si está desactivado, Lysara ignora los powerups del mapa.")]
    public bool recogePowerups = true;

    [Tooltip("Si está desactivado, Lysara reacciona tarde a las bombas (solo huye cuando explotan, no antes). MÁS FÁCIL DE MATAR.")]
    public bool huirPreventivamente = true;

    [Tooltip("Probabilidad (0 a 1) de que Lysara cometa un error de cálculo y elija un movimiento subóptimo. Útil para bajar la dificultad sin desactivar capacidades.")]
    [Range(0f, 0.5f)]
    public float probabilidadError = 0f;

    [Header("═══ MOVIMIENTO ═══")]
    [Tooltip("Velocidad de movimiento en unidades por segundo.")]
    [Range(1f, 10f)]
    public float speed = 5f;

    [Tooltip("Segundos entre decisiones de movimiento. Menor = reacciona más rápido pero consume más CPU.")]
    [Range(0.08f, 0.4f)]
    public float timeBetweenMoves = 0.18f;

    [Tooltip("Variación aleatoria (+/- segundos) para que no parezca una máquina perfecta.")]
    [Range(0f, 0.1f)]
    public float timingJitter = 0.04f;

    [Tooltip("Segundos de espera antes de que la IA empiece a actuar al arrancar el nivel.")]
    [Range(0f, 10f)]
    public float delayInicio = 3f;

    [Header("═══ VIDAS ═══")]
    [Tooltip("Número de vidas antes de morir.")]
    [Range(1, 10)]
    public int Vidas = 3;

    [Header("═══ COMBATE ═══")]
    [Tooltip("Probabilidad de colocar bomba cuando detecta oportunidad (0 = nunca ataca, 1 = siempre ataca).")]
    [Range(0f, 1f)]
    public float bombChance = 0.90f;

    [Tooltip("Probabilidad de tomar un camino alternativo al perseguir (le da imprevisibilidad).")]
    [Range(0f, 0.3f)]
    public float wanderChance = 0.08f;

    [Header("═══ SUPERVIVENCIA (crítico) ═══")]
    [Tooltip("Distancia mínima en celdas que debe recorrer al huir de su propia bomba. Mayor = más seguro.")]
    [Range(1.5f, 5f)]
    public float distanciaEscapeMinima = 2.5f;

    [Tooltip("Segundos de colchón que debe sobrar tras llegar al escape. Mayor = más seguro.")]
    [Range(0.2f, 2f)]
    public float margenSeguridadEscape = 0.6f;

    [Tooltip("Si escapar tarda más de (fuseTime - este valor), NO coloca bomba. Mayor = más prudente.")]
    [Range(0.3f, 2f)]
    public float umbralAbortoBomba = 0.8f;

    [Tooltip("Segundos restantes de una bomba para considerarla inminente y huir ya.")]
    [Range(0.5f, 3f)]
    public float umbralBombaInminente = 1.5f;

    [Tooltip("Segundos en modo defensivo tras recibir daño (no ataca, mantiene distancia).")]
    [Range(0f, 10f)]
    public float modoDefensivoTrasDano = 3f;

    [Header("═══ PERSECUCIÓN ═══")]
    [Tooltip("Cuánto en el futuro predecir la posición de Aurora (segundos).")]
    [Range(0f, 1f)]
    public float tiempoPrediccionAurora = 0.3f;

    [Tooltip("Distancia en celdas a mantener de Aurora cuando está en modo defensivo.")]
    [Range(2f, 8f)]
    public float distanciaDefensiva = 4.5f;

    [Tooltip("Mínimo de salidas caminables que debe tener una celda objetivo al perseguir.")]
    [Range(1, 4)]
    public int minimoSalidasObjetivo = 2;

    [Header("═══ ACORRALAMIENTO ═══")]
    [Tooltip("Máximo de salidas que puede tener Aurora para que Lysara intente acorralarla.")]
    [Range(1, 4)]
    public int maxSalidasAuroraParaAcorralar = 2;

    [Tooltip("Distancia máxima (celdas) a la que intenta acorralar a Aurora.")]
    [Range(2f, 10f)]
    public float distanciaMaxAcorralar = 6f;

    [Header("═══ AVANZADO (BFS) ═══")]
    [Tooltip("Máximo de celdas a explorar en una búsqueda. Mayor = más inteligente pero más lento.")]
    [Range(100, 800)]
    public int limiteBFS = 400;

    [Tooltip("Cada cuántos segundos refrescar el cache de bombas en el mapa.")]
    [Range(0.05f, 0.3f)]
    public float intervaloRefrescoCache = 0.08f;

    [Header("═══ SPRITES ═══")]
    public SpritesAnimadosRender spriteRendererUp;
    public SpritesAnimadosRender spriteRendererDown;
    public SpritesAnimadosRender spriteRendererLeft;
    public SpritesAnimadosRender spriteRendererRight;
    public SpritesAnimadosRender spriteRenderDeath;
    public SpritesAnimadosRender spriteRenderDamage;

    [Header("═══ DEBUG (solo lectura) ═══")]
    [Tooltip("Estado actual de la IA. Se actualiza en tiempo real.")]
    public EstadoIA estado = EstadoIA.Idle;

    // ─────────────────────────────────────────────────────────────────────────
    // FIN INSPECTOR
    // ─────────────────────────────────────────────────────────────────────────

    public enum EstadoIA { Idle, Perseguir, Acorralar, Huir, Bomba, Powerup, Recuperando, Defensivo }

    public enum NivelInteligencia
    {
        Principiante,   // IA muy lenta, comete errores frecuentes, casi no ataca
        Facil,          // Reacciona lento, ataca ocasionalmente
        Normal,         // Balance estándar
        Dificil,        // Agresiva y rápida
        Experto,        // Implacable: máxima velocidad, predicción, cero errores
        Personalizado   // No aplica preset, usa los valores del inspector tal cual
    }

    // Aplica automáticamente los valores del preset sobre los parámetros del inspector.
    // Se llama en Awake si aplicarPresetAutomatico está activo.
    private void AplicarPreset()
    {
        switch (nivelInteligencia)
        {
            case NivelInteligencia.Principiante:
                timeBetweenMoves = 0.35f;
                bombChance = 0.30f;
                wanderChance = 0.35f;
                probabilidadError = 0.35f;
                distanciaEscapeMinima = 2f;
                margenSeguridadEscape = 0.4f;
                umbralAbortoBomba = 0.6f;
                umbralBombaInminente = 1.0f;
                modoDefensivoTrasDano = 6f;
                tiempoPrediccionAurora = 0f;
                distanciaDefensiva = 5.5f;
                minimoSalidasObjetivo = 1;
                maxSalidasAuroraParaAcorralar = 1;
                distanciaMaxAcorralar = 3f;
                usarPrediccionAurora = false;
                puedeAcorralar = false;
                huirPreventivamente = false;
                puedeRomperBloques = false;
                break;

            case NivelInteligencia.Facil:
                timeBetweenMoves = 0.26f;
                bombChance = 0.55f;
                wanderChance = 0.20f;
                probabilidadError = 0.18f;
                distanciaEscapeMinima = 2.2f;
                margenSeguridadEscape = 0.5f;
                umbralAbortoBomba = 0.7f;
                umbralBombaInminente = 1.2f;
                modoDefensivoTrasDano = 4.5f;
                tiempoPrediccionAurora = 0.15f;
                distanciaDefensiva = 5f;
                minimoSalidasObjetivo = 2;
                maxSalidasAuroraParaAcorralar = 1;
                distanciaMaxAcorralar = 4f;
                usarPrediccionAurora = false;
                puedeAcorralar = true;
                huirPreventivamente = true;
                puedeRomperBloques = true;
                break;

            case NivelInteligencia.Normal:
                timeBetweenMoves = 0.18f;
                bombChance = 0.80f;
                wanderChance = 0.10f;
                probabilidadError = 0.08f;
                distanciaEscapeMinima = 2.5f;
                margenSeguridadEscape = 0.6f;
                umbralAbortoBomba = 0.8f;
                umbralBombaInminente = 1.5f;
                modoDefensivoTrasDano = 3f;
                tiempoPrediccionAurora = 0.3f;
                distanciaDefensiva = 4.5f;
                minimoSalidasObjetivo = 2;
                maxSalidasAuroraParaAcorralar = 2;
                distanciaMaxAcorralar = 6f;
                usarPrediccionAurora = true;
                puedeAcorralar = true;
                huirPreventivamente = true;
                puedeRomperBloques = true;
                break;

            case NivelInteligencia.Dificil:
                timeBetweenMoves = 0.14f;
                bombChance = 0.92f;
                wanderChance = 0.05f;
                probabilidadError = 0.02f;
                distanciaEscapeMinima = 2f;
                margenSeguridadEscape = 0.5f;
                umbralAbortoBomba = 0.6f;
                umbralBombaInminente = 1.8f;
                modoDefensivoTrasDano = 1.5f;
                tiempoPrediccionAurora = 0.45f;
                distanciaDefensiva = 4f;
                minimoSalidasObjetivo = 2;
                maxSalidasAuroraParaAcorralar = 3;
                distanciaMaxAcorralar = 8f;
                usarPrediccionAurora = true;
                puedeAcorralar = true;
                huirPreventivamente = true;
                puedeRomperBloques = true;
                break;

            case NivelInteligencia.Experto:
                timeBetweenMoves = 0.10f;
                bombChance = 1.0f;
                wanderChance = 0f;
                probabilidadError = 0f;
                distanciaEscapeMinima = 2f;
                margenSeguridadEscape = 0.4f;
                umbralAbortoBomba = 0.5f;
                umbralBombaInminente = 2.2f;
                modoDefensivoTrasDano = 0.5f;
                tiempoPrediccionAurora = 0.6f;
                distanciaDefensiva = 3.5f;
                minimoSalidasObjetivo = 1;
                maxSalidasAuroraParaAcorralar = 3;
                distanciaMaxAcorralar = 10f;
                usarPrediccionAurora = true;
                puedeAcorralar = true;
                huirPreventivamente = true;
                puedeRomperBloques = true;
                break;

            case NivelInteligencia.Personalizado:
                // No modifica nada: respeta los valores del inspector
                break;
        }
    }

    public new Rigidbody2D rigidbody { get; private set; }
    private SpritesAnimadosRender activeSpriteRenderer;

    private ControlBomba controlBomba;
    private bool iaActiva = false;
    private bool isInvincible = false;
    private bool enRecuperacion = false;
    private bool bombaPuesta = false;
    private float tiempoFinModoDefensivo = 0f;
    private bool EnModoDefensivo => Time.time < tiempoFinModoDefensivo;

    private Vector2 posicionAnterior;
    private float tiempoAtasco = 0f;
    private const float LIM_ATASCO = 0.8f;
    private const float UMBRAL = 0.05f;

    private readonly Queue<Vector2> memoriaCeldas = new Queue<Vector2>();
    private const int MEMORIA_SIZE = 10;

    private readonly Dictionary<Vector2, float> memoriaCalor = new Dictionary<Vector2, float>();

    private Vector2 auroraVelocidad = Vector2.zero;
    private Vector2 auroraPosAnterior;
    private float auroraTiempoAnterior;

    private struct BombaRastreada
    {
        public Vector2 pos;
        public float tiempoPrimerAvistamiento;
        public GameObject objeto;
    }
    private readonly Dictionary<GameObject, BombaRastreada> bombasRastreadas = new Dictionary<GameObject, BombaRastreada>();
    private float proximaActualizacionCache = 0f;

    private LayerMask maskExp;
    private LayerMask maskInd;
    private LayerMask maskDes;
    private LayerMask maskTodo;

    private int capaExp = -1;
    private int CapaExp
    {
        get { if (capaExp < 0) capaExp = LayerMask.NameToLayer("Explosion"); return capaExp; }
    }

    private float CalorDuracion =>
        controlBomba != null
            ? controlBomba.bombFuseTime + controlBomba.explosionDuration + 0.5f
            : 5f;

    private Coroutine _corBuclePrincipal;
    private bool abortarMovimiento = false;

    // ═════════════════════════════════════════════════════════════════════════
    // INICIO
    // ═════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (aplicarPresetAutomatico) AplicarPreset();
        rigidbody = GetComponent<Rigidbody2D>();
        controlBomba = GetComponent<ControlBomba>();
        activeSpriteRenderer = spriteRendererDown;
        maskExp = LayerMask.GetMask("Explosion");
        maskInd = LayerMask.GetMask("TilesIndestructibles");
        maskDes = LayerMask.GetMask("TilesDestructibles");
        maskTodo = maskInd | maskDes;
    }

#if UNITY_EDITOR
    // Se llama cuando cambias un valor en el Inspector (incluso sin entrar a Play).
    // Así ves en tiempo real cómo afecta cambiar el nivel a los demás parámetros.
    private void OnValidate()
    {
        if (aplicarPresetAutomatico && nivelInteligencia != NivelInteligencia.Personalizado)
            AplicarPreset();
    }
#endif

    private void Start() => StartCoroutine(Iniciar());

    private IEnumerator Iniciar()
    {
        rigidbody.position = Snap(rigidbody.position);
        yield return new WaitForSeconds(delayInicio);
        posicionAnterior = rigidbody.position;
        if (Aurora != null)
        {
            auroraPosAnterior = Aurora.position;
            auroraTiempoAnterior = Time.time;
        }
        iaActiva = true;
        _corBuclePrincipal = StartCoroutine(BuclePrincipal());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RASTREO DE BOMBAS
    // ═════════════════════════════════════════════════════════════════════════

    private void ActualizarRastreoBombas()
    {
        if (Time.time < proximaActualizacionCache) return;
        proximaActualizacionCache = Time.time + intervaloRefrescoCache;

        var bombasActuales = GameObject.FindGameObjectsWithTag("Bomba");
        var vistasAhora = new HashSet<GameObject>();

        foreach (var b in bombasActuales)
        {
            if (b == null || !b.activeInHierarchy) continue;
            vistasAhora.Add(b);

            if (!bombasRastreadas.ContainsKey(b))
            {
                bombasRastreadas[b] = new BombaRastreada
                {
                    pos = Snap(b.transform.position),
                    tiempoPrimerAvistamiento = Time.time,
                    objeto = b
                };
            }
        }

        var claves = new List<GameObject>(bombasRastreadas.Keys);
        foreach (var k in claves)
            if (k == null || !vistasAhora.Contains(k))
                bombasRastreadas.Remove(k);
    }

    private float TiempoRestanteBomba(BombaRastreada b)
    {
        float fuse = controlBomba != null ? controlBomba.bombFuseTime : 3f;
        float transcurrido = Time.time - b.tiempoPrimerAvistamiento;
        return Mathf.Max(0f, fuse - transcurrido);
    }

    private void ActualizarVelocidadAurora()
    {
        if (Aurora == null) return;
        float dt = Time.time - auroraTiempoAnterior;
        if (dt < 0.08f) return;
        Vector2 posActual = Aurora.position;
        auroraVelocidad = (posActual - auroraPosAnterior) / dt;
        auroraPosAnterior = posActual;
        auroraTiempoAnterior = Time.time;
    }

    private Vector2 PredecirAurora(float segundos)
    {
        if (Aurora == null) return Vector2.zero;
        Vector2 actual = Aurora.position;
        if (auroraVelocidad.sqrMagnitude < 0.5f) return Snap(actual);
        Vector2 predicha = actual + auroraVelocidad * segundos;
        if (!EsCaminable(Snap(predicha))) return Snap(actual);
        return Snap(predicha);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUCLE PRINCIPAL
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator BuclePrincipal()
    {
        while (true)
        {
            float espera = timeBetweenMoves + Random.Range(-timingJitter, timingJitter);
            yield return new WaitForSeconds(Mathf.Max(0.06f, espera));
            if (!iaActiva) yield break;

            ActualizarRastreoBombas();
            ActualizarVelocidadAurora();
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

            // ══════════════════════════════════════════════════════════════
            // PRIORIDAD 1 — SUPERVIVENCIA
            // ══════════════════════════════════════════════════════════════

            // Si huirPreventivamente está desactivado, ignoramos la detección temprana de bombas
            bool huir = HayPeligro(yo);
            if (huirPreventivamente) huir |= PeligroInminente(yo) || AisladaPorBombas(yo);
            if (huir)
            {
                estado = EstadoIA.Huir;
                yield return StartCoroutine(HuirTactico(yo));
                continue;
            }

            // ══════════════════════════════════════════════════════════════
            // PRIORIDAD 2 — ATAQUE (solo si completamente seguro)
            // ══════════════════════════════════════════════════════════════

            if (!enRecuperacion && !EnModoDefensivo && !bombaPuesta
                && puedeColocarBombas
                && controlBomba != null && controlBomba.enabled)
            {
                DecisionBomba decision = EvaluarDecisionBomba(yo);

                if (decision.debeColocar && Random.value < bombChance)
                {
                    List<Vector2> rutaEscape = BuscarRutaEscapeValida(yo);
                    if (rutaEscape != null && rutaEscape.Count > 0
                        && EscapeSigueSiendoSeguro(rutaEscape))
                    {
                        estado = EstadoIA.Bomba;
                        bombaPuesta = true;
                        RegistrarCalor(yo, CalorDuracion);
                        controlBomba.TryPlaceBomb();
                        // Forzar refresco inmediato del rastreo para registrar la bomba recién puesta
                        proximaActualizacionCache = 0f;
                        ActualizarRastreoBombas();
                        yield return new WaitForSeconds(0.02f);
                        // CRÍTICO: usar SeguirRutaEscape (no SeguirRuta), porque la ruta
                        // entera empieza dentro del radio de la bomba recién colocada
                        yield return StartCoroutine(SeguirRutaEscape(rutaEscape));
                        yield return StartCoroutine(EsperarZonaLimpia());
                        bombaPuesta = false;
                        continue;
                    }
                }

                if (decision.debeAcorralar)
                {
                    estado = EstadoIA.Acorralar;
                    Vector2 sig = PrimerPaso(yo, decision.celdaBloqueo);
                    if (sig != Vector2.zero && CeldaSegura(sig))
                    {
                        yield return StartCoroutine(MoverATile(sig));
                        continue;
                    }
                }
            }

            GameObject pup = recogePowerups ? PowerupSeguro(7f) : null;
            if (pup != null)
            {
                estado = EstadoIA.Powerup;
                Vector2 sig = PrimerPaso(yo, Snap(pup.transform.position));
                if (sig != Vector2.zero && CeldaSegura(sig))
                {
                    yield return StartCoroutine(MoverATile(sig));
                    continue;
                }
            }

            if (Aurora != null)
            {
                estado = EnModoDefensivo ? EstadoIA.Defensivo : EstadoIA.Perseguir;
                Vector2 auroraPredicha = usarPrediccionAurora
                    ? PredecirAurora(tiempoPrediccionAurora)
                    : Snap(Aurora.position);
                Vector2 objetivo = EnModoDefensivo
                    ? ObjetivoDefensivo(yo, auroraPredicha)
                    : ObjetivoPresion(yo, auroraPredicha);
                Vector2 siguiente = PrimerPaso(yo, objetivo);

                // probabilidadError: a veces elige mal a propósito
                if (siguiente != Vector2.zero && Random.value < probabilidadError)
                {
                    Vector2 alt = CeldaAlternativa(yo, siguiente);
                    if (alt != Vector2.zero) siguiente = alt;
                }
                else if (siguiente != Vector2.zero && Random.value < wanderChance)
                {
                    Vector2 alt = CeldaAlternativa(yo, siguiente);
                    if (alt != Vector2.zero) siguiente = alt;
                }

                if (siguiente != Vector2.zero && CeldaSegura(siguiente))
                {
                    yield return StartCoroutine(MoverATile(siguiente));
                    continue;
                }
            }

            Vector2 libre = CeldaLibreConMemoria(yo);
            if (libre != Vector2.zero && CeldaSegura(libre))
                yield return StartCoroutine(MoverATile(libre));
        }
    }

    private bool CeldaSegura(Vector2 c)
    {
        if (HayPeligro(c)) return false;
        if (PeligroInminente(c)) return false;
        if (CeldaEnCalor(c)) return false;
        return true;
    }

    private bool AisladaPorBombas(Vector2 yo)
    {
        int salidasLimpias = 0;
        foreach (Vector2 d in Dirs())
        {
            Vector2 c = yo + d;
            if (!EsCaminable(c)) continue;
            if (!HayPeligro(c) && !PeligroInminente(c)) salidasLimpias++;
        }
        if (salidasLimpias == 0)
        {
            foreach (var kv in bombasRastreadas)
            {
                float restante = TiempoRestanteBomba(kv.Value);
                if (restante > 2.5f) continue;
                int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
                if (ZonaBomba(kv.Value.pos, radio).Contains(yo)) return true;
            }
        }
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DECISIÓN DE BOMBA
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

        if (Aurora == null) return d;
        Vector2 posA = Snap(Aurora.position);
        Vector2 posAPredicha = usarPrediccionAurora
            ? PredecirAurora(controlBomba != null ? controlBomba.bombFuseTime * 0.6f : 1.5f)
            : posA;

        if (AuroraEnLinea(yo)) { d.debeColocar = true; return d; }
        if (usarPrediccionAurora && AuroraEnLineaDesde(yo, posAPredicha))
        { d.debeColocar = true; return d; }

        float dist = ManhattanDist(yo, posA);
        if (dist <= r + 0.6f) { d.debeColocar = true; return d; }

        // Destructibles solo si tiene permitido romperlos
        if (puedeRomperBloques)
        {
            if (DestructibleQueExpondriaAurora(yo)) { d.debeColocar = true; return d; }
            if (DestructibleHaciaAurora(yo)) { d.debeColocar = true; return d; }
        }

        // Acorralamiento solo si tiene la capacidad activada
        if (puedeAcorralar)
        {
            int salidasAurora = ContarSalidas(posA);
            if (salidasAurora <= maxSalidasAuroraParaAcorralar && dist <= distanciaMaxAcorralar)
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

        return d;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // VALIDACIÓN ESTRICTA DE ESCAPE
    // ═════════════════════════════════════════════════════════════════════════

    private List<Vector2> BuscarRutaEscapeValida(Vector2 desde)
    {
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        float fuseTime = controlBomba != null ? controlBomba.bombFuseTime : 3f;
        float tiempoMaxEscape = fuseTime - umbralAbortoBomba;
        if (tiempoMaxEscape <= 0) return null;

        HashSet<Vector2> peligro = ZonaBomba(desde, radio);
        foreach (var kv in bombasRastreadas)
            foreach (var c in ZonaBomba(kv.Value.pos, radio))
                peligro.Add(c);

        Queue<Vector2> cola = new Queue<Vector2>();
        Dictionary<Vector2, Vector2> padre = new Dictionary<Vector2, Vector2>();
        Dictionary<Vector2, int> distancia = new Dictionary<Vector2, int>();

        cola.Enqueue(desde);
        padre[desde] = desde;
        distancia[desde] = 0;

        Vector2 mejorDest = Vector2.zero;
        float mejorScore = float.MinValue;

        int explorados = 0;
        while (cola.Count > 0 && explorados++ < limiteBFS)
        {
            Vector2 actual = cola.Dequeue();
            int dist = distancia[actual];
            float tiempoNecesario = dist * timeBetweenMoves;

            bool segura = !peligro.Contains(actual) && !CeldaEnCalor(actual);
            bool lejosSuficiente = Vector2.Distance(desde, actual) >= distanciaEscapeMinima;
            bool aTiempo = tiempoNecesario + margenSeguridadEscape <= tiempoMaxEscape;

            if (actual != desde && segura && lejosSuficiente && aTiempo)
            {
                float score = 0f;
                score += ContarSalidas(actual) * 30f;
                score -= dist * 2f;
                score += Vector2.Distance(desde, actual) * 4f;
                score += DistBomba(actual) * 5f;
                if (score > mejorScore) { mejorScore = score; mejorDest = actual; }
            }

            foreach (Vector2 dir in Dirs())
            {
                Vector2 v = actual + dir;
                if (padre.ContainsKey(v)) continue;
                if (!EsCaminable(v)) continue;
                if (HayPeligro(v)) continue;
                padre[v] = actual;
                distancia[v] = dist + 1;
                cola.Enqueue(v);
            }
        }

        if (mejorDest == Vector2.zero) return null;

        List<Vector2> ruta = new List<Vector2>();
        Vector2 paso = mejorDest;
        while (paso != desde)
        {
            ruta.Add(paso);
            paso = padre[paso];
        }
        ruta.Reverse();
        return ruta;
    }

    // Sanity check pre-bomba: la ruta no debe pasar por explosión activa.
    // NO chequeamos HayPeligro (radio de bomba) porque la propia bomba que vamos
    // a colocar meterá todas las celdas del radio en ese estado.
    private bool EscapeSigueSiendoSeguro(List<Vector2> ruta)
    {
        if (ruta == null || ruta.Count == 0) return false;
        foreach (var c in ruta)
            if (ExplosionActivaEn(c)) return false;
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DETECCIÓN DE PELIGRO
    // ═════════════════════════════════════════════════════════════════════════

    private bool HayPeligro(Vector2 celda)
    {
        if (Physics2D.OverlapCircle(celda, 0.42f, maskExp) != null) return true;
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        foreach (var kv in bombasRastreadas)
            if (ZonaBomba(kv.Value.pos, radio).Contains(celda)) return true;
        return false;
    }

    private bool PeligroInminente(Vector2 celda)
    {
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        foreach (var kv in bombasRastreadas)
        {
            float restante = TiempoRestanteBomba(kv.Value);
            if (restante > umbralBombaInminente) continue;
            if (ZonaBomba(kv.Value.pos, radio).Contains(celda)) return true;
        }
        return false;
    }

    private HashSet<Vector2> ZonasActivasDePeligro(int radio)
    {
        var z = new HashSet<Vector2>();
        foreach (var kv in bombasRastreadas)
            foreach (var c in ZonaBomba(kv.Value.pos, radio)) z.Add(c);
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
        foreach (var kv in bombasRastreadas)
        {
            float d = Vector2.Distance(desde, kv.Value.pos);
            if (d < min) min = d;
        }
        return min == float.MaxValue ? 999f : min;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HUIDA TÁCTICA
    //
    // IMPORTANTE (fix v3.2):
    //  · Distinguimos "ExplosionActiva" (fuego real en la celda = muerte inmediata)
    //    de "HayPeligro" (en radio de una bomba con mecha = peligro futuro).
    //  · Al huir de una bomba recién colocada, la ruta COMPLETA atraviesa zona
    //    en radio de bomba; eso es normal y no debe abortar la huida.
    //  · Solo abortamos y recalculamos si aparece una explosión REAL en el camino.
    //  · Sin recursión mutua: ninguna función de seguir-ruta vuelve a llamar HuirTactico.
    // ═════════════════════════════════════════════════════════════════════════

    // Solo detecta explosiones activas (fuego real).
    // Esto es lo ÚNICO que debe abortar una huida en curso.
    private bool ExplosionActivaEn(Vector2 celda)
    {
        return Physics2D.OverlapCircle(celda, 0.42f, maskExp) != null;
    }

    private IEnumerator HuirTactico(Vector2 desde)
    {
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;
        HashSet<Vector2> peligro = ZonasActivasDePeligro(radio);
        Vector2 destino = CeldaMasSeguraTactica(desde, peligro);

        if (destino == desde)
        {
            // Fallback 1: cualquier vecina fuera de zona de peligro
            foreach (Vector2 d in DirsAleatorio())
            {
                Vector2 c = desde + d;
                if (EsCaminable(c) && !peligro.Contains(c))
                {
                    destino = c;
                    break;
                }
            }
            // Fallback 2: cualquier vecina caminable que no sea fuego directo
            if (destino == desde)
            {
                foreach (Vector2 d in DirsAleatorio())
                {
                    Vector2 c = desde + d;
                    if (EsCaminable(c) && !ExplosionActivaEn(c)) { destino = c; break; }
                }
                if (destino == desde) yield break;
            }
        }

        List<Vector2> ruta = RutaBFSEscape(desde, destino);
        if (ruta == null || ruta.Count == 0) yield break;

        yield return StartCoroutine(SeguirRutaEscape(ruta));
    }

    // Ruta de huida: se ejecuta COMPLETA salvo que aparezca fuego real.
    // NO aborta por "estar en radio de bomba" (eso es lo normal al huir de tu propia bomba).
    private IEnumerator SeguirRutaEscape(List<Vector2> ruta)
    {
        if (ruta == null) yield break;
        foreach (Vector2 paso in ruta)
        {
            // Solo abortamos si hay FUEGO ACTIVO en el siguiente paso (muerte instantánea)
            if (ExplosionActivaEn(paso)) yield break;
            yield return StartCoroutine(MoverATile(paso));
            if (abortarMovimiento) { abortarMovimiento = false; yield break; }
        }
    }

    // Rutas no-escape (persecución, acorralamiento, powerups):
    // SÍ aborta ante peligro potencial porque no debería estar metiéndose ahí.
    private IEnumerator SeguirRuta(List<Vector2> ruta)
    {
        if (ruta == null) yield break;
        foreach (Vector2 paso in ruta)
        {
            if (HayPeligro(paso) || CeldaEnCalor(paso)) yield break;
            yield return StartCoroutine(MoverATile(paso));
            if (abortarMovimiento) { abortarMovimiento = false; yield break; }
        }
    }

    private IEnumerator EsperarZonaLimpia()
    {
        float tMax = controlBomba != null
            ? controlBomba.bombFuseTime + controlBomba.explosionDuration + 1.0f
            : 5f;
        float t = 0f;
        // Limitamos iteraciones para prevenir bucles infinitos
        int maxIter = 30;
        int iter = 0;
        while (t < tMax && iter++ < maxIter)
        {
            Vector2 yo = Snap(transform.position);
            if (!HayPeligro(yo) && !PeligroInminente(yo)) yield break;
            yield return StartCoroutine(HuirTactico(yo));
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

        while (cola.Count > 0 && visitadas.Count < limiteBFS)
        {
            Vector2 actual = cola.Dequeue();

            float score = 0f;
            if (!peligro.Contains(actual)) score += 300f;
            if (!CeldaEnCalor(actual)) score += 80f;
            score += ContarSalidas(actual) * 35f;
            score += DistBomba(actual) * 10f;
            if (memoriaCeldas.Contains(actual)) score -= 12f;
            score += Random.Range(-5f, 5f);

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
    // PERSECUCIÓN
    // ═════════════════════════════════════════════════════════════════════════

    private Vector2 ObjetivoPresion(Vector2 yo, Vector2 posAurora)
    {
        if (Aurora == null) return yo;
        int radio = controlBomba != null ? controlBomba.explosionRadius : 2;

        Vector2 mejor = posAurora;
        float mejorScore = float.MinValue;

        foreach (Vector2 dir in Dirs())
        {
            for (int r = 1; r <= radio + 1; r++)
            {
                Vector2 c = posAurora + dir * r;
                if (EsIndestructible(c)) break;
                if (!EsCaminable(c) || HayPeligro(c) || CeldaEnCalor(c)) continue;
                if (ContarSalidas(c) < minimoSalidasObjetivo) continue;

                float score = 0f;
                if (AuroraEnLineaDesde(c, posAurora)) score += 100f;
                score -= Vector2.Distance(yo, c) * 5f;
                score -= ContarSalidas(posAurora) * 8f;
                score += (4 - ContarSalidas(c)) * 8f;
                score += ContarSalidas(c) * 15f;
                foreach (var kv in bombasRastreadas)
                    if (ZonaBomba(kv.Value.pos, radio).Contains(c)) score -= 100f;

                if (score > mejorScore) { mejorScore = score; mejor = c; }
            }
        }
        return mejor;
    }

    private Vector2 ObjetivoDefensivo(Vector2 yo, Vector2 posAurora)
    {
        if (Aurora == null) return yo;

        Vector2 mejor = yo;
        float mejorScore = float.MinValue;
        Queue<Vector2> cola = new Queue<Vector2>();
        HashSet<Vector2> vis = new HashSet<Vector2>();
        cola.Enqueue(yo); vis.Add(yo);

        while (cola.Count > 0 && vis.Count < 200)
        {
            Vector2 a = cola.Dequeue();
            float distAurora = Vector2.Distance(a, posAurora);

            if (distAurora >= distanciaDefensiva && !HayPeligro(a) && !CeldaEnCalor(a)
                && ContarSalidas(a) >= minimoSalidasObjetivo)
            {
                float score = 0f;
                score -= Mathf.Abs(distAurora - distanciaDefensiva) * 5f;
                score += ContarSalidas(a) * 10f;
                score -= Vector2.Distance(yo, a) * 2f;
                if (score > mejorScore) { mejorScore = score; mejor = a; }
            }

            foreach (Vector2 d in Dirs())
            {
                Vector2 v = a + d;
                if (!vis.Contains(v) && EsCaminable(v)) { vis.Add(v); cola.Enqueue(v); }
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
        while (cola.Count > 0 && padre.Count < limiteBFS)
        {
            Vector2Int c = cola.Dequeue();
            if (c == g) { ok = true; break; }
            foreach (var d in dirs)
            {
                Vector2Int v = c + d;
                if (padre.ContainsKey(v)) continue;
                Vector2 w = new Vector2(v.x, v.y);
                bool segura = v == g || (EsCaminable(w) && !HayPeligro(w)
                                          && !PeligroInminente(w) && !CeldaEnCalor(w));
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

    // BFS permisivo para emergencias: evita explosión activa pero puede pasar
    // por peligro inminente o calor si no hay mejor alternativa
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
        while (cola.Count > 0 && padre.Count < limiteBFS)
        {
            Vector2Int c = cola.Dequeue();
            if (c == g) { ok = true; break; }
            foreach (var d in dirs)
            {
                Vector2Int v = c + d;
                if (padre.ContainsKey(v)) continue;
                Vector2 w = new Vector2(v.x, v.y);
                // Permitimos pasar por peligro inminente/calor, pero NUNCA por explosión activa
                bool libre = v == g || (EsCaminable(w) && !HayPeligro(w));
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
    // ACORRALAMIENTO
    // ═════════════════════════════════════════════════════════════════════════

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
                    HashSet<Vector2> peligro = ZonaBomba(c, r);
                    bool hayEscape = false;
                    foreach (Vector2 dd in Dirs())
                    {
                        Vector2 cc = c + dd;
                        if (EsCaminable(cc) && !peligro.Contains(cc) && !CeldaEnCalor(cc))
                        { hayEscape = true; break; }
                    }
                    if (!hayEscape) continue;

                    float dist = Vector2.Distance(yo, c);
                    if (dist < menorD) { menorD = dist; mejor = c; }
                }
            }
        }
        return mejor;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DETECCIÓN DE AURORA
    // ═════════════════════════════════════════════════════════════════════════

    private bool AuroraEnLinea(Vector2 yo)
    {
        if (Aurora == null) return false;
        return AuroraEnLineaDesde(yo, Snap(Aurora.position));
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

    // ═════════════════════════════════════════════════════════════════════════
    // ANÁLISIS
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
    // POWERUPS
    // ═════════════════════════════════════════════════════════════════════════

    private GameObject PowerupSeguro(float radio)
    {
        GameObject mejor = null;
        float min = Mathf.Infinity;
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, radio))
        {
            if (!h.CompareTag("Powerup")) continue;
            Vector2 cp = Snap(h.transform.position);
            if (HayPeligro(cp) || PeligroInminente(cp) || CeldaEnCalor(cp)) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < min) { min = d; mejor = h.gameObject; }
        }
        return mejor;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CALOR
    // ═════════════════════════════════════════════════════════════════════════

    private void RegistrarCalor(Vector2 centro, float duracion = -1f)
    {
        if (duracion < 0f) duracion = CalorDuracion;
        float expira = Time.time + duracion;
        int r = controlBomba != null ? controlBomba.explosionRadius : 2;

        memoriaCalor[centro] = expira;

        foreach (Vector2 dir in Dirs())
            for (int i = 1; i <= r; i++)
            {
                Vector2 c = centro + dir * i;
                if (EsIndestructible(c)) break;
                if (!memoriaCalor.TryGetValue(c, out float prev) || expira > prev)
                    memoriaCalor[c] = expira;
                if (EsDestructible(c)) break;
            }
    }

    private bool CeldaEnCalor(Vector2 c) =>
        memoriaCalor.TryGetValue(c, out float t) && Time.time < t;

    private void LimpiarCalor()
    {
        if (memoriaCalor.Count == 0) return;
        var exp = new List<Vector2>();
        foreach (var kv in memoriaCalor) if (Time.time >= kv.Value) exp.Add(kv.Key);
        foreach (var k in exp) memoriaCalor.Remove(k);
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
            if (CeldaSegura(c) && EsCaminable(c)) return c;
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
            if (!EsCaminable(c) || !CeldaSegura(c)) continue;

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
            if (EsCaminable(c) && CeldaSegura(c))
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
    // MOVIMIENTO con vigilancia continua
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator MoverATile(Vector2 dest)
    {
        dest = Snap(dest);
        float t = 0f;
        float tMax = (Vector2.Distance(rigidbody.position, dest) / Mathf.Max(0.5f, speed)) + 0.4f;
        float chequeoSeguridad = 0f;
        const float INTERVALO_CHEQUEO = 0.05f;

        while (Vector2.Distance(rigidbody.position, dest) > UMBRAL)
        {
            t += Time.fixedDeltaTime;
            chequeoSeguridad += Time.fixedDeltaTime;

            if (chequeoSeguridad >= INTERVALO_CHEQUEO)
            {
                chequeoSeguridad = 0f;
                ActualizarRastreoBombas();
                Vector2 yoAhora = Snap(rigidbody.position);

                // SOLO abortamos si hay FUEGO ACTIVO en el destino (muerte segura).
                // "Estar en radio de bomba" NO debe abortar — es normal al huir
                // de una bomba propia: toda la ruta empieza dentro del radio.
                if (ExplosionActivaEn(dest))
                {
                    abortarMovimiento = true;
                    rigidbody.position = Snap(rigidbody.position);
                    SetSprite(Vector2.zero);
                    yield break;
                }
                // Si estoy en fuego activo Y el destino también: estoy atrapada,
                // abortar para que el BuclePrincipal busque ruta alternativa
                if (ExplosionActivaEn(yoAhora) && ExplosionActivaEn(dest))
                {
                    abortarMovimiento = true;
                    rigidbody.position = Snap(rigidbody.position);
                    SetSprite(Vector2.zero);
                    yield break;
                }
            }

            if (t >= tMax)
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

    // Apaga absolutamente TODOS los sprites para evitar que se superpongan.
    // Crítico antes de activar damage o death para no ver dos animaciones a la vez.
    private void OcultarTodosLosSprites()
    {
        if (spriteRendererUp != null) spriteRendererUp.enabled = false;
        if (spriteRendererDown != null) spriteRendererDown.enabled = false;
        if (spriteRendererLeft != null) spriteRendererLeft.enabled = false;
        if (spriteRendererRight != null) spriteRendererRight.enabled = false;
        if (spriteRenderDamage != null) spriteRenderDamage.enabled = false;
        if (spriteRenderDeath != null) spriteRenderDeath.enabled = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DAÑO Y MUERTE
    // ═════════════════════════════════════════════════════════════════════════

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

        iaActiva = true;
        bombaPuesta = false;
        _corBuclePrincipal = StartCoroutine(BuclePrincipal());
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