using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════════
//  LYSARA — IA v4.0 — AGRESIVA + OPTIMIZADA — Vombelle
// ═══════════════════════════════════════════════════════════════════════════════
//
//  ÍNDICE DE SECCIONES (en orden de aparición):
//
//  ┌─[ INSPECTOR ]──────────────────────────────────────────────────────────┐
//  │ Variables públicas configurables desde Unity, agrupadas por header:    │
//  │   · REFERENCIAS — el Transform de Aurora.                              │
//  │   · NIVEL DE INTELIGENCIA — preset (Principiante a Experto).           │
//  │   · ESTILO DE JUEGO — sliders maestros: agresividad e escape.          │
//  │   · CAPACIDADES — toggles (puede bombear, predicción, acorralar...).   │
//  │   · MOVIMIENTO, VIDAS, COMBATE, SUPERVIVENCIA, PERSECUCIÓN,            │
//  │     ACORRALAMIENTO, BFS, SPRITES, DEBUG.                               │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ ENUMS Y PRESETS ]────────────────────────────────────────────────────┐
//  │ EstadoIA y NivelInteligencia. AplicarPreset() ajusta todos los         │
//  │ parámetros según el nivel elegido.                                     │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ PROPIEDADES DERIVADAS ]──────────────────────────────────────────────┐
//  │ Cálculos efectivos a partir de los sliders maestros:                   │
//  │   · DistanciaEscapeEfectiva, BombChanceEfectiva, MultiplicadorPresion. │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ CAMPOS PRIVADOS Y CACHE ]────────────────────────────────────────────┐
//  │ Estado interno: rigidbody, controlBomba, banderas (iaActiva,           │
//  │ isInvincible...), memoria de calor, rastreo de bombas con timestamps,  │
//  │ cache por tick de zonas de peligro (evita recalcular).                 │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ INICIO ]─────────────────────────────────────────────────────────────┐
//  │ Awake/OnValidate/Start/Iniciar — setup, presets, cachés de máscaras.   │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ RASTREO DE BOMBAS Y AURORA ]─────────────────────────────────────────┐
//  │ Mantiene un diccionario de bombas con cuándo aparecieron,              │
//  │ velocidad estimada de Aurora y predicción de su posición futura.       │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ BUCLE PRINCIPAL ]────────────────────────────────────────────────────┐
//  │ Decisión por tick. PRIORIDAD: ataque > huida (excepto fuego activo).   │
//  │ Orden: anti-atasco → fuego inminente → ATAQUE → powerup → persecución. │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ DECISIÓN DE BOMBA ]──────────────────────────────────────────────────┐
//  │ EvaluarDecisionBomba() — 7 razones para colocar bomba ordenadas        │
//  │ por valor estratégico. Acorralamiento si Aurora tiene pocas salidas.   │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ VALIDACIÓN DE ESCAPE ]───────────────────────────────────────────────┐
//  │ BuscarRutaEscapeValida() — BFS que solo devuelve ruta si garantiza     │
//  │ celda segura + a tiempo + lejos suficiente. Si no, no se bombea.       │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ DETECCIÓN DE PELIGRO ]───────────────────────────────────────────────┐
//  │ HayPeligro / PeligroInminente / ExplosionActivaEn — niveles de         │
//  │ urgencia para decidir cuándo huir y cuándo arriesgarse a atacar.       │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ HUIDA TÁCTICA ]──────────────────────────────────────────────────────┐
//  │ HuirTactico → SeguirRutaEscape. Sin recursión. Recorta ruta según      │
//  │ intensidadEscape para no irse al rincón cuando no es necesario.        │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ PERSECUCIÓN ]────────────────────────────────────────────────────────┐
//  │ ObjetivoPresion — busca celdas con línea de fuego a Aurora pegándose   │
//  │ o respirando según agresividad. ObjetivoDefensivo — kiting tras daño.  │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ BFS ]────────────────────────────────────────────────────────────────┐
//  │ RutaBFS (segura) y RutaBFSEscape (permisiva en emergencia). Algoritmo  │
//  │ de búsqueda en anchura para pathfinding por celdas.                    │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ ACORRALAMIENTO ]─────────────────────────────────────────────────────┐
//  │ EncontrarBloqueoOptimo — celda desde la que tendría línea a Aurora     │
//  │ y aún tendría escape disponible.                                       │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ DETECCIÓN DE AURORA ]────────────────────────────────────────────────┐
//  │ AuroraEnLinea, MuroEntre, DestructibleQueExpondriaAurora,              │
//  │ DestructibleHaciaAurora — chequeos geométricos sobre el tablero.       │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ ANÁLISIS / POWERUPS / CALOR ]────────────────────────────────────────┐
//  │ ContarSalidas, PowerupSeguro, RegistrarCalor (memoria de explosiones   │
//  │ recientes), LimpiarCalor (purga entradas vencidas).                    │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ VARIABILIDAD / DESATASCAR ]──────────────────────────────────────────┐
//  │ CeldaAlternativa, CeldaLibreConMemoria, Desatascar — para no parecer   │
//  │ una máquina y para salir de bloqueos físicos.                          │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ MOVIMIENTO ]─────────────────────────────────────────────────────────┐
//  │ MoverATile — mueve a la celda destino con vigilancia de fuego activo   │
//  │ durante el desplazamiento (puede abortar a mitad de camino).           │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ SPRITES ]────────────────────────────────────────────────────────────┐
//  │ SetSprite, OcultarMovimiento, OcultarTodosLosSprites — gestión de las  │
//  │ animaciones direccionales, daño y muerte sin solapamientos.            │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ DAÑO Y MUERTE ]──────────────────────────────────────────────────────┐
//  │ ProcesarExplosion, IniciarDano/TerminarDano, IniciarMuerte/FinMuerte.  │
//  │ Modo defensivo temporal tras recibir daño.                             │
//  └────────────────────────────────────────────────────────────────────────┘
//  ┌─[ UTILIDADES ]─────────────────────────────────────────────────────────┐
//  │ Dirs, EsCaminable, EsIndestructible, EsDestructible, Snap, VI.         │
//  └────────────────────────────────────────────────────────────────────────┘
//
//  OPTIMIZACIONES v4.0:
//   · Cache por tick de zonas de peligro (1 cálculo, N consultas).
//   · Reuso de HashSet/Queue para evitar GC.
//   · ZonaBomba reusa buffer estático.
//   · Bucles for en vez de foreach en hot paths.
//   · Salidas tempranas en validaciones.
//   · Arrays en vez de listas donde el tamaño es fijo.
//
//  PRIORIDAD AGRESIVIDAD v4.0:
//   · El ataque se evalúa ANTES que la huida preventiva.
//   · Solo el fuego activo o peligro extremo aborta el ataque.
//   · Si tiene oportunidad clara, ignora amenazas con >1.5s de mecha.
// ═══════════════════════════════════════════════════════════════════════════════

public class ControlMovimientoLysara : MonoBehaviour
{
    // ═════════════════════════════════════════════════════════════════════════
    // [ INSPECTOR ] — Configuración pública desde Unity
    // ═════════════════════════════════════════════════════════════════════════

    [Header("═══ REFERENCIAS ═══")]
    [Tooltip("Transform de la jugadora (Aurora). Arrástralo aquí desde la escena.")]
    public Transform Aurora;

    [Header("═══ NIVEL DE INTELIGENCIA ═══")]
    [Tooltip("Preset general de dificultad. PERSONALIZADO = control manual total.")]
    public NivelInteligencia nivelInteligencia = NivelInteligencia.Normal;

    [Tooltip("Si está marcado, al iniciar se aplican los valores del preset.")]
    public bool aplicarPresetAutomatico = true;

    [Header("═══ ESTILO DE JUEGO (sliders maestros) ═══")]
    [Tooltip("Qué tan paranoica es al escapar. 0 = se queda cerca tras bombear (FÁCIL DE ALCANZAR). 1 = huye al rincón más alejado (imposible de cazar pero nunca recibe daño).")]
    [Range(0f, 1f)]
    public float intensidadEscape = 0.5f;

    [Tooltip("Qué tan agresiva persigue y bombea. 0 = pasiva. 1 = obsesiva, presiona constantemente.")]
    [Range(0f, 1f)]
    public float nivelAgresividad = 0.5f;

    [Header("═══ CAPACIDADES (toggles individuales) ═══")]
    [Tooltip("Si está desactivado, NO coloca bombas (modo evasivo / tutorial).")]
    public bool puedeColocarBombas = true;

    [Tooltip("Si está desactivado, no anticipa los movimientos de Aurora.")]
    public bool usarPrediccionAurora = true;

    [Tooltip("Si está desactivado, no intenta acorralar a Aurora.")]
    public bool puedeAcorralar = true;

    [Tooltip("Si está desactivado, no rompe bloques destructibles.")]
    public bool puedeRomperBloques = true;

    [Tooltip("Si está desactivado, ignora los powerups del mapa.")]
    public bool recogePowerups = true;

    [Tooltip("Si está desactivado, reacciona tarde a las bombas. MÁS FÁCIL DE MATAR.")]
    public bool huirPreventivamente = true;

    [Tooltip("Probabilidad de cometer un error de cálculo (0 a 0.5).")]
    [Range(0f, 0.5f)]
    public float probabilidadError = 0f;

    [Header("═══ MOVIMIENTO ═══")]
    [Range(1f, 10f)] public float speed = 5f;
    [Range(0.08f, 0.4f)] public float timeBetweenMoves = 0.18f;
    [Range(0f, 0.1f)] public float timingJitter = 0.04f;
    [Range(0f, 10f)] public float delayInicio = 3f;

    [Header("═══ VIDAS ═══")]
    [Range(1, 10)] public int Vidas = 3;

    [Header("═══ COMBATE ═══")]
    [Tooltip("Probabilidad base de bombear. Se MULTIPLICA por nivelAgresividad.")]
    [Range(0f, 1f)] public float bombChance = 0.90f;
    [Range(0f, 0.3f)] public float wanderChance = 0.08f;

    [Header("═══ SUPERVIVENCIA (crítico) ═══")]
    [Tooltip("Distancia mínima de escape. Se MULTIPLICA por intensidadEscape (0.5x a 1.5x).")]
    [Range(1.5f, 5f)] public float distanciaEscapeMinima = 2.5f;
    [Range(0.2f, 2f)] public float margenSeguridadEscape = 0.6f;
    [Range(0.3f, 2f)] public float umbralAbortoBomba = 0.8f;
    [Range(0.5f, 3f)] public float umbralBombaInminente = 1.5f;
    [Range(0f, 10f)] public float modoDefensivoTrasDano = 3f;

    [Header("═══ PERSECUCIÓN ═══")]
    [Range(0f, 1f)] public float tiempoPrediccionAurora = 0.3f;
    [Range(2f, 8f)] public float distanciaDefensiva = 4.5f;
    [Range(1, 4)] public int minimoSalidasObjetivo = 2;

    [Tooltip("Distancia ideal (celdas) a Aurora durante la persecución. Menor = más cerca, más presión. Se recomienda 1-3.")]
    [Range(1f, 5f)] public float distanciaPersecucionIdeal = 2f;

    [Tooltip("Si está activado, Lysara persigue a Aurora SIEMPRE que no tenga otra acción prioritaria. Desactívalo solo si quieres que deambule cuando no hay oportunidad clara de ataque.")]
    public bool perseguirSiempre = true;

    [Header("═══ ACORRALAMIENTO ═══")]
    [Range(1, 4)] public int maxSalidasAuroraParaAcorralar = 2;
    [Range(2f, 12f)] public float distanciaMaxAcorralar = 6f;

    [Header("═══ AVANZADO (BFS) ═══")]
    [Range(100, 800)] public int limiteBFS = 400;
    [Range(0.05f, 0.3f)] public float intervaloRefrescoCache = 0.08f;

    [Header("═══ SPRITES ═══")]
    public SpritesAnimadosRender spriteRendererUp;
    public SpritesAnimadosRender spriteRendererDown;
    public SpritesAnimadosRender spriteRendererLeft;
    public SpritesAnimadosRender spriteRendererRight;
    public SpritesAnimadosRender spriteRenderDeath;
    public SpritesAnimadosRender spriteRenderDamage;

    [Header("═══ DEBUG ═══")]
    public EstadoIA estado = EstadoIA.Idle;

    // ═════════════════════════════════════════════════════════════════════════
    // [ ENUMS Y PRESETS ]
    // ═════════════════════════════════════════════════════════════════════════

    public enum EstadoIA { Idle, Perseguir, Acorralar, Huir, Bomba, Powerup, Recuperando, Defensivo }

    public enum NivelInteligencia
    {
        Principiante, Facil, Normal, Dificil, Experto, Personalizado
    }

    private void AplicarPreset()
    {
        switch (nivelInteligencia)
        {
            case NivelInteligencia.Principiante:
                intensidadEscape = 0.20f; nivelAgresividad = 0.20f;
                timeBetweenMoves = 0.32f; bombChance = 0.35f; wanderChance = 0.30f;
                probabilidadError = 0.35f; distanciaEscapeMinima = 1.5f;
                margenSeguridadEscape = 0.4f; umbralAbortoBomba = 0.5f;
                umbralBombaInminente = 0.8f; modoDefensivoTrasDano = 5f;
                tiempoPrediccionAurora = 0f; distanciaDefensiva = 3f;
                distanciaPersecucionIdeal = 2f; perseguirSiempre = true;
                minimoSalidasObjetivo = 1; maxSalidasAuroraParaAcorralar = 1;
                distanciaMaxAcorralar = 5f;
                usarPrediccionAurora = false; puedeAcorralar = false;
                huirPreventivamente = false; puedeRomperBloques = true;
                break;

            case NivelInteligencia.Facil:
                intensidadEscape = 0.25f; nivelAgresividad = 0.55f;
                timeBetweenMoves = 0.20f; bombChance = 0.80f; wanderChance = 0.10f;
                probabilidadError = 0.15f; distanciaEscapeMinima = 1.5f;
                margenSeguridadEscape = 0.35f; umbralAbortoBomba = 0.4f;
                umbralBombaInminente = 1.3f; modoDefensivoTrasDano = 2.5f;
                tiempoPrediccionAurora = 0.2f; distanciaDefensiva = 2f;
                distanciaPersecucionIdeal = 1.2f; perseguirSiempre = true;
                minimoSalidasObjetivo = 1; maxSalidasAuroraParaAcorralar = 2;
                distanciaMaxAcorralar = 8f;
                usarPrediccionAurora = true; puedeAcorralar = true;
                huirPreventivamente = true; puedeRomperBloques = true;
                break;

            case NivelInteligencia.Normal:
                intensidadEscape = 0.35f; nivelAgresividad = 0.85f;
                timeBetweenMoves = 0.13f; bombChance = 0.95f; wanderChance = 0.04f;
                probabilidadError = 0.04f; distanciaEscapeMinima = 1.8f;
                margenSeguridadEscape = 0.4f; umbralAbortoBomba = 0.45f;
                umbralBombaInminente = 1.6f; modoDefensivoTrasDano = 1.8f;
                tiempoPrediccionAurora = 0.35f; distanciaDefensiva = 1.5f;
                distanciaPersecucionIdeal = 1f; perseguirSiempre = true;
                minimoSalidasObjetivo = 1; maxSalidasAuroraParaAcorralar = 3;
                distanciaMaxAcorralar = 15f;
                usarPrediccionAurora = true; puedeAcorralar = true;
                huirPreventivamente = true; puedeRomperBloques = true;
                break;

            case NivelInteligencia.Dificil:
                intensidadEscape = 0.45f; nivelAgresividad = 1f;
                timeBetweenMoves = 0.10f; bombChance = 1f; wanderChance = 0.02f;
                probabilidadError = 0f; distanciaEscapeMinima = 1.8f;
                margenSeguridadEscape = 0.35f; umbralAbortoBomba = 0.4f;
                umbralBombaInminente = 1.9f; modoDefensivoTrasDano = 1f;
                tiempoPrediccionAurora = 0.5f; distanciaDefensiva = 1.2f;
                distanciaPersecucionIdeal = 1f; perseguirSiempre = true;
                minimoSalidasObjetivo = 1; maxSalidasAuroraParaAcorralar = 3;
                distanciaMaxAcorralar = 20f;
                usarPrediccionAurora = true; puedeAcorralar = true;
                huirPreventivamente = true; puedeRomperBloques = true;
                break;

            case NivelInteligencia.Experto:
                intensidadEscape = 0.55f; nivelAgresividad = 1f;
                timeBetweenMoves = 0.08f; bombChance = 1f; wanderChance = 0f;
                probabilidadError = 0f; distanciaEscapeMinima = 1.8f;
                margenSeguridadEscape = 0.35f; umbralAbortoBomba = 0.35f;
                umbralBombaInminente = 2.3f; modoDefensivoTrasDano = 0.2f;
                tiempoPrediccionAurora = 0.65f; distanciaDefensiva = 1f;
                distanciaPersecucionIdeal = 1f; perseguirSiempre = true;
                minimoSalidasObjetivo = 1; maxSalidasAuroraParaAcorralar = 3;
                distanciaMaxAcorralar = 30f;
                usarPrediccionAurora = true; puedeAcorralar = true;
                huirPreventivamente = true; puedeRomperBloques = true;
                break;

            case NivelInteligencia.Personalizado:
                break;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ PROPIEDADES DERIVADAS ] — calculadas a partir de los sliders maestros
    // ═════════════════════════════════════════════════════════════════════════

    // intensidadEscape modula la distancia de escape en un rango conservador.
    // v4.1: rango reducido (0.7x a 1.2x en vez de 0.5x a 1.5x) para que
    // ni siquiera al máximo se aleje demasiado de Aurora tras bombear.
    private float DistanciaEscapeEfectiva =>
        distanciaEscapeMinima * Mathf.Lerp(0.7f, 1.2f, intensidadEscape);

    private float BombChanceEfectiva =>
        Mathf.Clamp01(bombChance * Mathf.Lerp(0.3f, 1.2f, nivelAgresividad));

    private float MultiplicadorPresion => Mathf.Lerp(0.4f, 1.5f, nivelAgresividad);

    // ═════════════════════════════════════════════════════════════════════════
    // [ CAMPOS PRIVADOS Y CACHE ]
    // ═════════════════════════════════════════════════════════════════════════

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
    private const int MEMORIA_SIZE = 10;

    // Memoria de calor: celdas peligrosas tras explosiones recientes
    private readonly Dictionary<Vector2, float> memoriaCalor = new Dictionary<Vector2, float>(64);
    private readonly Queue<Vector2> memoriaCeldas = new Queue<Vector2>(MEMORIA_SIZE);

    // Predicción de Aurora
    private Vector2 auroraVelocidad = Vector2.zero;
    private Vector2 auroraPosAnterior;
    private float auroraTiempoAnterior;

    // Rastreo de bombas con timestamp de primer avistamiento
    private struct BombaRastreada
    {
        public Vector2 pos;
        public float tiempoPrimerAvistamiento;
        public GameObject objeto;
    }
    private readonly Dictionary<GameObject, BombaRastreada> bombasRastreadas = new Dictionary<GameObject, BombaRastreada>(16);
    private float proximaActualizacionCache = 0f;

    // OPT: Cache por tick de zonas de peligro (se reconstruye cuando cambian las bombas)
    private readonly HashSet<Vector2> _cachePeligro = new HashSet<Vector2>();
    private float _cachePeligroTimestamp = -1f;
    private int _cachePeligroBombasCount = -1;

    // OPT: Buffers reutilizables para evitar allocaciones
    private readonly HashSet<Vector2> _bufferZona = new HashSet<Vector2>();
    private readonly Queue<Vector2> _bufferCola = new Queue<Vector2>(128);
    private readonly HashSet<Vector2> _bufferVisitadas = new HashSet<Vector2>();
    private readonly List<GameObject> _bufferLimpieza = new List<GameObject>(16);
    private readonly List<Vector2> _bufferLimpiezaCalor = new List<Vector2>(32);

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

    // OPT: Cache del radio de explosión (se actualiza al inicio de cada tick)
    private int _radioExplosion = 2;

    // ═════════════════════════════════════════════════════════════════════════
    // [ INICIO ]
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
    // [ RASTREO DE BOMBAS Y AURORA ]
    // ═════════════════════════════════════════════════════════════════════════

    private void ActualizarRastreoBombas()
    {
        if (Time.time < proximaActualizacionCache) return;
        proximaActualizacionCache = Time.time + intervaloRefrescoCache;

        var bombasActuales = GameObject.FindGameObjectsWithTag("Bomba");
        _bufferLimpieza.Clear();

        // OPT: marcar las que siguen vivas usando un timestamp en lugar de un HashSet temporal
        float tNow = Time.time;
        for (int i = 0; i < bombasActuales.Length; i++)
        {
            var b = bombasActuales[i];
            if (b == null || !b.activeInHierarchy) continue;

            if (!bombasRastreadas.ContainsKey(b))
            {
                bombasRastreadas[b] = new BombaRastreada
                {
                    pos = Snap(b.transform.position),
                    tiempoPrimerAvistamiento = tNow,
                    objeto = b
                };
            }
        }

        // Detectar bombas que ya no existen
        foreach (var kv in bombasRastreadas)
        {
            bool encontrada = false;
            for (int i = 0; i < bombasActuales.Length; i++)
            {
                if (bombasActuales[i] == kv.Key) { encontrada = true; break; }
            }
            if (!encontrada || kv.Key == null) _bufferLimpieza.Add(kv.Key);
        }
        for (int i = 0; i < _bufferLimpieza.Count; i++)
            bombasRastreadas.Remove(_bufferLimpieza[i]);

        // Invalidar cache de peligro al haber cambios
        _cachePeligroTimestamp = -1f;
    }

    private float TiempoRestanteBomba(BombaRastreada b)
    {
        float fuse = controlBomba != null ? controlBomba.bombFuseTime : 3f;
        return Mathf.Max(0f, fuse - (Time.time - b.tiempoPrimerAvistamiento));
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
    // [ BUCLE PRINCIPAL ] — orden: AGRESIVIDAD primero, supervivencia segunda
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

            // OPT: cachear radio para todo el tick
            _radioExplosion = controlBomba != null ? controlBomba.explosionRadius : 2;

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
            // RED DE SEGURIDAD MÍNIMA — solo huir ante peligro extremo
            // (fuego activo en mi celda, o aislada sin salidas)
            // El resto del peligro lo gestionará el chequeo posterior
            // permitiendo atacar primero si hay oportunidad clara.
            // ══════════════════════════════════════════════════════════════
            if (ExplosionActivaEn(yo) || AisladaPorBombas(yo))
            {
                estado = EstadoIA.Huir;
                yield return StartCoroutine(HuirTactico(yo));
                continue;
            }

            // ══════════════════════════════════════════════════════════════
            // PRIORIDAD 1 — ATAQUE
            // Si hay oportunidad clara de bombear y se garantiza escape,
            // se hace AUNQUE haya bombas con mecha en el radio.
            // ══════════════════════════════════════════════════════════════
            bool ataqueRealizado = false;
            if (!enRecuperacion && !EnModoDefensivo && !bombaPuesta
                && puedeColocarBombas
                && controlBomba != null && controlBomba.enabled)
            {
                DecisionBomba decision = EvaluarDecisionBomba(yo);

                if (decision.debeColocar && Random.value < BombChanceEfectiva)
                {
                    List<Vector2> rutaEscape = BuscarRutaEscapeValida(yo);
                    if (rutaEscape != null && rutaEscape.Count > 0
                        && EscapeSigueSiendoSeguro(rutaEscape))
                    {
                        estado = EstadoIA.Bomba;
                        bombaPuesta = true;
                        RegistrarCalor(yo, CalorDuracion);
                        controlBomba.TryPlaceBomb();
                        proximaActualizacionCache = 0f;
                        ActualizarRastreoBombas();
                        yield return new WaitForSeconds(0.02f);
                        yield return StartCoroutine(SeguirRutaEscape(rutaEscape));
                        // v4.1: ya no esperamos a que la zona esté "limpia".
                        // En cuanto terminamos el escape, el bucle vuelve a evaluar:
                        // si hay peligro huirá, si no, perseguirá a Aurora inmediatamente.
                        bombaPuesta = false;
                        ataqueRealizado = true;
                    }
                }
                else if (decision.debeAcorralar)
                {
                    estado = EstadoIA.Acorralar;
                    Vector2 sig = PrimerPaso(yo, decision.celdaBloqueo);
                    if (sig != Vector2.zero && CeldaSegura(sig))
                    {
                        yield return StartCoroutine(MoverATile(sig));
                        ataqueRealizado = true;
                    }
                }
            }
            if (ataqueRealizado) continue;

            // ══════════════════════════════════════════════════════════════
            // PRIORIDAD 2 — HUIDA PREVENTIVA
            // Solo entra aquí si no se pudo atacar.
            // ══════════════════════════════════════════════════════════════
            if (HayPeligro(yo) || (huirPreventivamente && PeligroInminente(yo)))
            {
                estado = EstadoIA.Huir;
                yield return StartCoroutine(HuirTactico(yo));
                continue;
            }

            // ── 3. POWERUP cercano ────────────────────────────────────────
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

            // ── 4. PERSECUCIÓN — va DIRECTO hacia Aurora siempre ─────────
            // v4.2: La meta por defecto es la posición exacta de Aurora.
            // Solo usamos ObjetivoPresion si la ruta directa está bloqueada o
            // si ya estamos muy cerca y queremos mantener línea de fuego.
            if (Aurora != null && (perseguirSiempre || nivelAgresividad >= 0.3f))
            {
                estado = EnModoDefensivo ? EstadoIA.Defensivo : EstadoIA.Perseguir;
                Vector2 auroraPredicha = usarPrediccionAurora
                    ? PredecirAurora(tiempoPrediccionAurora)
                    : Snap(Aurora.position);

                // En modo defensivo: respetar distancia mínima
                Vector2 objetivo;
                if (EnModoDefensivo)
                {
                    objetivo = ObjetivoDefensivo(yo, auroraPredicha);
                }
                else
                {
                    float distAurora = Vector2.Distance(yo, auroraPredicha);
                    // Si estoy lejos: ir DIRECTO a Aurora (mejor ruta = ruta más corta)
                    // Si ya estoy muy cerca (distancia ≤ ideal+1): buscar celda con línea de fuego
                    if (distAurora > distanciaPersecucionIdeal + 1f)
                        objetivo = auroraPredicha;
                    else
                        objetivo = ObjetivoPresion(yo, auroraPredicha);
                }

                Vector2 siguiente = PrimerPaso(yo, objetivo);

                // Si la ruta falló, intentar ir directamente a Aurora
                if (siguiente == Vector2.zero)
                    siguiente = PrimerPaso(yo, auroraPredicha);

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

                if (siguiente != Vector2.zero && !ExplosionActivaEn(siguiente))
                {
                    yield return StartCoroutine(MoverATile(siguiente));
                    continue;
                }

                // Fallback: paso inmediato que más acerque a Aurora
                Vector2 acercamiento = MejorPasoHaciaAurora(yo, auroraPredicha);
                if (acercamiento != Vector2.zero && !ExplosionActivaEn(acercamiento))
                {
                    yield return StartCoroutine(MoverATile(acercamiento));
                    continue;
                }
            }

            // ── 5. Deambular ──────────────────────────────────────────────
            Vector2 libre = CeldaLibreConMemoria(yo);
            if (libre != Vector2.zero && CeldaSegura(libre))
                yield return StartCoroutine(MoverATile(libre));
        }
    }

    private bool CeldaSegura(Vector2 c)
    {
        return !HayPeligro(c) && !PeligroInminente(c) && !CeldaEnCalor(c);
    }

    private bool AisladaPorBombas(Vector2 yo)
    {
        // OPT: salida temprana
        int salidasLimpias = 0;
        for (int i = 0; i < _dirs.Length; i++)
        {
            Vector2 c = yo + _dirs[i];
            if (!EsCaminable(c)) continue;
            if (!HayPeligro(c) && !PeligroInminente(c))
            {
                salidasLimpias++;
                if (salidasLimpias > 0) return false; // tengo al menos una salida limpia
            }
        }
        // Sin salidas: chequear si estoy en zona de bomba próxima a explotar
        foreach (var kv in bombasRastreadas)
        {
            if (TiempoRestanteBomba(kv.Value) > 2.5f) continue;
            if (CeldaEnZonaBomba(yo, kv.Value.pos)) return true;
        }
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ DECISIÓN DE BOMBA ]
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
        if (Aurora == null) return d;

        Vector2 posA = Snap(Aurora.position);
        Vector2 posAPredicha = usarPrediccionAurora
            ? PredecirAurora(controlBomba != null ? controlBomba.bombFuseTime * 0.6f : 1.5f)
            : posA;

        // R1: Aurora en línea de fuego AHORA → bomba inmediata
        if (AuroraEnLinea(yo)) { d.debeColocar = true; return d; }

        // R2: Aurora ESTARÁ en línea cuando explote (predicción)
        if (usarPrediccionAurora && AuroraEnLineaDesde(yo, posAPredicha))
        { d.debeColocar = true; return d; }

        float dist = ManhattanDist(yo, posA);

        // R3: Aurora MUY cerca (distancia Manhattan ≤ radio+1). Bomba inmediata.
        // v4.2: ampliamos el rango — si está a ≤ radio+1.5, bombeamos aunque
        // no haya línea directa todavía (puede haber destructibles en medio).
        if (dist <= _radioExplosion + 1.5f) { d.debeColocar = true; return d; }

        // R4: bomba de proximidad con alta agresividad
        // Si Aurora está a ≤ 3.5 celdas Y mi agresividad es alta, bombeo
        // como medida de presión — la fuerzo a moverse.
        if (nivelAgresividad >= 0.7f && dist <= 3.5f)
        { d.debeColocar = true; return d; }

        if (puedeRomperBloques)
        {
            if (DestructibleQueExpondriaAurora(yo)) { d.debeColocar = true; return d; }
            if (DestructibleHaciaAurora(yo)) { d.debeColocar = true; return d; }
        }

        if (puedeAcorralar)
        {
            int maxSalidas = maxSalidasAuroraParaAcorralar
                + (nivelAgresividad > 0.7f ? 1 : 0);
            float distMax = distanciaMaxAcorralar * Mathf.Lerp(0.6f, 1.4f, nivelAgresividad);

            int salidasAurora = ContarSalidas(posA);
            if (salidasAurora <= maxSalidas && dist <= distMax)
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
    // [ VALIDACIÓN DE ESCAPE ]
    // ═════════════════════════════════════════════════════════════════════════

    // Busca una ruta de escape VÁLIDA antes de colocar una bomba.
    // v4.1: hace DOS pasadas — primero intenta el escape estricto con todas
    // las garantías. Si falla (mucha paranoia), intenta un escape mínimo viable
    // que solo exige salir del radio de explosión antes de que detone.
    private List<Vector2> BuscarRutaEscapeValida(Vector2 desde)
    {
        float fuseTime = controlBomba != null ? controlBomba.bombFuseTime : 3f;

        // ── PRIMERA PASADA: escape estricto (el de siempre) ──
        var rutaEstricta = BuscarRutaEscapeInterna(desde, fuseTime, DistanciaEscapeEfectiva,
            margenSeguridadEscape, umbralAbortoBomba, exigirFueraPeligro: true);
        if (rutaEstricta != null) return rutaEstricta;

        // ── SEGUNDA PASADA: escape mínimo viable ──
        // Lysara acepta bombear aunque el escape no sea ideal, siempre que:
        //   · Salga del radio de explosión de SU propia bomba
        //   · Tenga un pequeño margen de tiempo (fuseTime - 0.3s)
        // Esto permite bombear agresivamente en escenarios de proximidad
        // sin volverse suicida: la celda destino sigue siendo una que NO explota.
        return BuscarRutaEscapeInterna(desde, fuseTime, 1.5f,
            0.25f, 0.3f, exigirFueraPeligro: true);
    }

    private List<Vector2> BuscarRutaEscapeInterna(Vector2 desde, float fuseTime,
        float distMinReal, float margenSeg, float umbralAborto, bool exigirFueraPeligro)
    {
        float tiempoMaxEscape = fuseTime - umbralAborto;
        if (tiempoMaxEscape <= 0) return null;

        // Zona peligrosa: SOLO la bomba que vamos a colocar + bombas existentes
        // (no incluimos calor ni bombas remotas irrelevantes)
        var peligro = new HashSet<Vector2>(32);
        AgregarZonaBomba(peligro, desde);
        foreach (var kv in bombasRastreadas)
            AgregarZonaBomba(peligro, kv.Value.pos);

        var cola = new Queue<Vector2>(32);
        var padre = new Dictionary<Vector2, Vector2>(32);
        var distancia = new Dictionary<Vector2, int>(32);

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

            bool fueraPeligro = !peligro.Contains(actual);
            bool lejosSuficiente = Vector2.Distance(desde, actual) >= distMinReal;
            bool aTiempo = tiempoNecesario + margenSeg <= tiempoMaxEscape;

            bool valida = actual != desde && fueraPeligro && lejosSuficiente && aTiempo;

            if (valida)
            {
                float score = 0f;
                score += ContarSalidas(actual) * 20f;
                // Preferimos rutas cortas (menos tiempo expuesta)
                score -= dist * 4f;
                score += DistBomba(actual) * 3f;
                if (score > mejorScore) { mejorScore = score; mejorDest = actual; }
            }

            for (int i = 0; i < _dirs.Length; i++)
            {
                Vector2 v = actual + _dirs[i];
                if (padre.ContainsKey(v)) continue;
                if (!EsCaminable(v)) continue;
                if (HayPeligro(v)) continue;
                padre[v] = actual;
                distancia[v] = dist + 1;
                cola.Enqueue(v);
            }
        }

        if (mejorDest == Vector2.zero) return null;

        var ruta = new List<Vector2>(8);
        Vector2 paso = mejorDest;
        while (paso != desde)
        {
            ruta.Add(paso);
            paso = padre[paso];
        }
        ruta.Reverse();
        return ruta;
    }

    private bool EscapeSigueSiendoSeguro(List<Vector2> ruta)
    {
        if (ruta == null || ruta.Count == 0) return false;
        for (int i = 0; i < ruta.Count; i++)
            if (ExplosionActivaEn(ruta[i])) return false;
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ DETECCIÓN DE PELIGRO ]
    // ═════════════════════════════════════════════════════════════════════════

    private bool ExplosionActivaEn(Vector2 celda)
    {
        return Physics2D.OverlapCircle(celda, 0.42f, maskExp) != null;
    }

    // OPT: si la celda está marcada como peligro O hay fuego activo
    private bool HayPeligro(Vector2 celda)
    {
        if (Physics2D.OverlapCircle(celda, 0.42f, maskExp) != null) return true;
        // Comprobar si está en zona de alguna bomba
        foreach (var kv in bombasRastreadas)
            if (CeldaEnZonaBomba(celda, kv.Value.pos)) return true;
        return false;
    }

    private bool PeligroInminente(Vector2 celda)
    {
        foreach (var kv in bombasRastreadas)
        {
            if (TiempoRestanteBomba(kv.Value) > umbralBombaInminente) continue;
            if (CeldaEnZonaBomba(celda, kv.Value.pos)) return true;
        }
        return false;
    }

    // OPT: comprueba si una celda está en la zona de una bomba SIN construir el HashSet completo
    private bool CeldaEnZonaBomba(Vector2 celda, Vector2 centroBomba)
    {
        if (celda == centroBomba) return true;
        // Solo puede estar en línea recta del centro
        if (celda.x != centroBomba.x && celda.y != centroBomba.y) return false;

        Vector2 dir;
        int distancia;
        if (celda.x == centroBomba.x)
        {
            distancia = Mathf.Abs((int)(celda.y - centroBomba.y));
            dir = celda.y > centroBomba.y ? Vector2.up : Vector2.down;
        }
        else
        {
            distancia = Mathf.Abs((int)(celda.x - centroBomba.x));
            dir = celda.x > centroBomba.x ? Vector2.right : Vector2.left;
        }

        if (distancia > _radioExplosion) return false;

        // Verificar que no haya muros bloqueando
        for (int i = 1; i <= distancia; i++)
        {
            Vector2 c = centroBomba + dir * i;
            if (EsIndestructible(c)) return false; // muro bloquea antes de llegar
            if (c == celda) return true;
            if (EsDestructible(c)) return false; // destructible bloquea antes
        }
        return false;
    }

    // Versión que SÍ construye el set (necesaria para algunos cálculos completos)
    private void AgregarZonaBomba(HashSet<Vector2> destino, Vector2 centro)
    {
        destino.Add(centro);
        for (int dirIdx = 0; dirIdx < _dirs.Length; dirIdx++)
        {
            Vector2 dir = _dirs[dirIdx];
            for (int i = 1; i <= _radioExplosion; i++)
            {
                Vector2 c = centro + dir * i;
                if (EsIndestructible(c)) break;
                destino.Add(c);
                if (EsDestructible(c)) break;
            }
        }
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
    // [ HUIDA TÁCTICA ]
    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator HuirTactico(Vector2 desde)
    {
        // Construir set de peligro UNA vez para esta huida
        var peligro = new HashSet<Vector2>(32);
        foreach (var kv in bombasRastreadas)
            AgregarZonaBomba(peligro, kv.Value.pos);

        Vector2 destino = CeldaMasSeguraTactica(desde, peligro);

        if (destino == desde)
        {
            // Fallback 1: vecina fuera de peligro
            for (int i = 0; i < _dirs.Length; i++)
            {
                Vector2 c = desde + _dirs[i];
                if (EsCaminable(c) && !peligro.Contains(c))
                {
                    destino = c;
                    break;
                }
            }
            // Fallback 2: vecina sin fuego activo
            if (destino == desde)
            {
                for (int i = 0; i < _dirs.Length; i++)
                {
                    Vector2 c = desde + _dirs[i];
                    if (EsCaminable(c) && !ExplosionActivaEn(c)) { destino = c; break; }
                }
                if (destino == desde) yield break;
            }
        }

        List<Vector2> ruta = RutaBFSEscape(desde, destino);
        if (ruta == null || ruta.Count == 0) yield break;

        // Recortar ruta: tras huir, queremos estar DENTRO del alcance de Aurora
        // para contraatacar rápido. v4.1: recorte más agresivo, solo salimos
        // del peligro + 1-2 pasos de colchón, nunca al rincón más lejano.
        if (ruta.Count > 2)
        {
            int corteMin = 1;
            for (int i = 0; i < ruta.Count; i++)
            {
                if (!peligro.Contains(ruta[i])) { corteMin = i + 1; break; }
            }
            // El colchón extra depende de intensidadEscape (0-2 pasos extra)
            int colchon = Mathf.RoundToInt(Mathf.Lerp(0f, 2f, intensidadEscape));
            int corte = Mathf.Min(ruta.Count, corteMin + colchon);
            if (corte < ruta.Count)
                ruta = ruta.GetRange(0, corte);
        }

        yield return StartCoroutine(SeguirRutaEscape(ruta));
    }

    private IEnumerator SeguirRutaEscape(List<Vector2> ruta)
    {
        if (ruta == null) yield break;
        for (int i = 0; i < ruta.Count; i++)
        {
            if (ExplosionActivaEn(ruta[i])) yield break;
            yield return StartCoroutine(MoverATile(ruta[i]));
            if (abortarMovimiento) { abortarMovimiento = false; yield break; }
        }
    }

    private IEnumerator SeguirRuta(List<Vector2> ruta)
    {
        if (ruta == null) yield break;
        for (int i = 0; i < ruta.Count; i++)
        {
            if (HayPeligro(ruta[i]) || CeldaEnCalor(ruta[i])) yield break;
            yield return StartCoroutine(MoverATile(ruta[i]));
            if (abortarMovimiento) { abortarMovimiento = false; yield break; }
        }
    }

    private IEnumerator EsperarZonaLimpia()
    {
        float tMax = controlBomba != null
            ? controlBomba.bombFuseTime + controlBomba.explosionDuration + 1.0f
            : 5f;
        float t = 0f;
        int iter = 0;
        while (t < tMax && iter++ < 30)
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
        _bufferCola.Clear();
        _bufferVisitadas.Clear();
        _bufferCola.Enqueue(desde);
        _bufferVisitadas.Add(desde);

        Vector2 mejorCelda = desde;
        float mejorScore = float.MinValue;

        while (_bufferCola.Count > 0 && _bufferVisitadas.Count < limiteBFS)
        {
            Vector2 actual = _bufferCola.Dequeue();

            float score = 0f;
            if (!peligro.Contains(actual)) score += 300f;
            if (!CeldaEnCalor(actual)) score += 80f;
            score += ContarSalidas(actual) * 35f;
            float pesoDist = Mathf.Lerp(2f, 12f, intensidadEscape);
            score += DistBomba(actual) * pesoDist;
            if (memoriaCeldas.Contains(actual)) score -= 12f;
            score += Random.Range(-5f, 5f);

            if (score > mejorScore) { mejorScore = score; mejorCelda = actual; }

            for (int i = 0; i < _dirs.Length; i++)
            {
                Vector2 v = actual + _dirs[i];
                if (!_bufferVisitadas.Contains(v) && EsCaminable(v))
                { _bufferVisitadas.Add(v); _bufferCola.Enqueue(v); }
            }
        }
        return mejorCelda;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ PERSECUCIÓN ]
    // ═════════════════════════════════════════════════════════════════════════

    // Busca la celda MÁS CERCANA a Aurora que sea alcanzable y segura.
    // La cercanía es lo que manda: entre dos celdas válidas, siempre la más pegada.
    // Solo se usan desempates (línea de fuego, salidas) cuando la distancia es igual.
    private Vector2 ObjetivoPresion(Vector2 yo, Vector2 posAurora)
    {
        if (Aurora == null) return yo;

        Vector2 mejor = posAurora;
        float mejorScore = float.MinValue;

        // Buscamos en un cuadrado alrededor de Aurora. Radio amplio para no perderla.
        int radioBusqueda = _radioExplosion + 3
            + Mathf.RoundToInt(Mathf.Lerp(0f, 3f, nivelAgresividad));

        for (int dx = -radioBusqueda; dx <= radioBusqueda; dx++)
        {
            for (int dy = -radioBusqueda; dy <= radioBusqueda; dy++)
            {
                Vector2 c = new Vector2(posAurora.x + dx, posAurora.y + dy);
                if (!EsCaminable(c) || HayPeligro(c) || CeldaEnCalor(c)) continue;

                float distAurora = Vector2.Distance(c, posAurora);

                // CORE: la cercanía domina. Cuanto más cerca de Aurora, mejor.
                // Penalizamos MUCHO la distancia a Aurora.
                float score = -distAurora * 100f;

                // Bonus si estamos a la distancia ideal O más cerca (¡mejor aún!)
                if (distAurora <= distanciaPersecucionIdeal)
                    score += 150f;

                // Bonus MUY fuerte por línea de fuego — esto le da preferencia
                // cuando hay varias celdas a distancia similar
                if (AuroraEnLineaDesde(c, posAurora)) score += 120f * MultiplicadorPresion;

                // Pequeña penalización por estar lejos de mi posición actual
                // (si puedo elegir entre dos celdas igual de cerca de Aurora,
                // prefiero la que me queda más cerca a mí)
                score -= Vector2.Distance(c, yo) * 2f;

                // Evitar celdas sin escape (muy importante para no suicidarse)
                if (ContarSalidas(c) == 0) score -= 500f;
                else score += ContarSalidas(c) * 3f;

                // Fuerte penalización si está en zona de bomba existente
                foreach (var kv in bombasRastreadas)
                    if (CeldaEnZonaBomba(c, kv.Value.pos)) { score -= 200f; break; }

                if (score > mejorScore) { mejorScore = score; mejor = c; }
            }
        }
        return mejor;
    }

    // Fallback insistente: escoge el vecino que más reduzca la distancia
    // a Aurora. Solo evita fuego activo — cualquier otra amenaza se gestiona
    // en el bucle principal.
    private Vector2 MejorPasoHaciaAurora(Vector2 yo, Vector2 posAurora)
    {
        Vector2 mejor = Vector2.zero;
        float mejorScore = float.MinValue;
        float distActual = Vector2.Distance(yo, posAurora);

        for (int i = 0; i < _dirs.Length; i++)
        {
            Vector2 c = yo + _dirs[i];
            if (!EsCaminable(c) || ExplosionActivaEn(c)) continue;

            float distNueva = Vector2.Distance(c, posAurora);
            float score = distActual - distNueva; // positivo = más cerca

            // Penalizar ligeramente si la celda está en radio de bomba
            // (aceptable pero no preferido)
            if (HayPeligro(c)) score -= 0.3f;

            // Bonus por línea de fuego cuando ya estamos cerca
            if (distActual <= distanciaPersecucionIdeal + 1f
                && AuroraEnLineaDesde(c, posAurora))
                score += 0.5f;

            if (score > mejorScore) { mejorScore = score; mejor = c; }
        }
        return mejor;
    }

    // Modo defensivo tras recibir daño: sigue persiguiendo a Aurora pero
    // manteniendo una distancia un poco mayor para no morir otra vez.
    // YA NO se aleja al rincón — solo respeta la distancia defensiva.
    private Vector2 ObjetivoDefensivo(Vector2 yo, Vector2 posAurora)
    {
        if (Aurora == null) return yo;

        Vector2 mejor = yo;
        float mejorScore = float.MinValue;
        _bufferCola.Clear();
        _bufferVisitadas.Clear();
        _bufferCola.Enqueue(yo);
        _bufferVisitadas.Add(yo);

        while (_bufferCola.Count > 0 && _bufferVisitadas.Count < 200)
        {
            Vector2 a = _bufferCola.Dequeue();
            float distAurora = Vector2.Distance(a, posAurora);

            if (!HayPeligro(a) && !CeldaEnCalor(a))
            {
                float score = 0f;
                // Preferir celdas EN o CERCA de la distancia defensiva (no más lejos)
                float errorDist = Mathf.Abs(distAurora - distanciaDefensiva);
                score -= errorDist * 15f;

                // Bonus fuerte por estar a distancia defensiva exacta
                if (distAurora >= distanciaDefensiva - 0.5f
                    && distAurora <= distanciaDefensiva + 1f)
                    score += 50f;

                score += ContarSalidas(a) * 8f;
                score -= Vector2.Distance(yo, a) * 2f;
                if (score > mejorScore) { mejorScore = score; mejor = a; }
            }

            for (int i = 0; i < _dirs.Length; i++)
            {
                Vector2 v = a + _dirs[i];
                if (!_bufferVisitadas.Contains(v) && EsCaminable(v))
                { _bufferVisitadas.Add(v); _bufferCola.Enqueue(v); }
            }
        }
        return mejor;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ BFS ] — pathfinding
    // ═════════════════════════════════════════════════════════════════════════

    private Vector2 PrimerPaso(Vector2 inicio, Vector2 meta)
    {
        var ruta = RutaBFS(inicio, meta);
        return ruta.Count > 0 ? ruta[0] : Vector2.zero;
    }

    // BFS principal usado para persecución y navegación general.
    // v4.2: solo evita peligro REAL (explosión activa + radio de bomba).
    // Ignora peligroInminente y calor — esas amenazas se gestionan con
    // chequeos previos en el bucle principal. Esto permite que Lysara
    // atraviese zonas "con mecha" si eso la lleva más rápido a Aurora.
    private List<Vector2> RutaBFS(Vector2 inicio, Vector2 meta)
    {
        Vector2Int s = VI(inicio), g = VI(meta);
        var result = new List<Vector2>(8);
        if (s == g) return result;

        var cola = new Queue<Vector2Int>(64);
        var padre = new Dictionary<Vector2Int, Vector2Int>(64);
        cola.Enqueue(s); padre[s] = s;

        bool ok = false;
        while (cola.Count > 0 && padre.Count < limiteBFS)
        {
            Vector2Int c = cola.Dequeue();
            if (c == g) { ok = true; break; }
            for (int i = 0; i < _dirsInt.Length; i++)
            {
                Vector2Int v = c + _dirsInt[i];
                if (padre.ContainsKey(v)) continue;
                Vector2 w = new Vector2(v.x, v.y);
                // Permisivo: solo muros y peligro directo (radio de bomba) nos bloquean
                bool segura = v == g || (EsCaminable(w) && !HayPeligro(w));
                if (segura) { padre[v] = c; cola.Enqueue(v); }
            }
        }
        if (!ok) return result;

        Vector2Int paso = g;
        while (paso != s) { result.Add(new Vector2(paso.x, paso.y)); paso = padre[paso]; }
        result.Reverse();
        return result;
    }

    private List<Vector2> RutaBFSEscape(Vector2 inicio, Vector2 meta)
    {
        Vector2Int s = VI(inicio), g = VI(meta);
        var result = new List<Vector2>(8);
        if (s == g) return result;

        var cola = new Queue<Vector2Int>(64);
        var padre = new Dictionary<Vector2Int, Vector2Int>(64);
        cola.Enqueue(s); padre[s] = s;

        bool ok = false;
        while (cola.Count > 0 && padre.Count < limiteBFS)
        {
            Vector2Int c = cola.Dequeue();
            if (c == g) { ok = true; break; }
            for (int i = 0; i < _dirsInt.Length; i++)
            {
                Vector2Int v = c + _dirsInt[i];
                if (padre.ContainsKey(v)) continue;
                Vector2 w = new Vector2(v.x, v.y);
                bool libre = v == g || (EsCaminable(w) && !HayPeligro(w));
                if (libre) { padre[v] = c; cola.Enqueue(v); }
            }
        }
        if (!ok) return result;

        Vector2Int paso = g;
        while (paso != s) { result.Add(new Vector2(paso.x, paso.y)); paso = padre[paso]; }
        result.Reverse();
        return result;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ ACORRALAMIENTO ]
    // ═════════════════════════════════════════════════════════════════════════

    private Vector2 EncontrarBloqueoOptimo(Vector2 yo, Vector2 posAurora)
    {
        Vector2 mejor = yo;
        float menorD = Mathf.Infinity;

        for (int dirIdx = 0; dirIdx < _dirs.Length; dirIdx++)
        {
            Vector2 dir = _dirs[dirIdx];
            for (int i = 1; i <= _radioExplosion + 2; i++)
            {
                Vector2 c = posAurora + dir * i;
                if (EsIndestructible(c)) break;
                if (!EsCaminable(c)) continue;

                if (AuroraEnLineaDesde(c, posAurora))
                {
                    // OPT: comprobar escape SIN construir HashSet
                    bool hayEscape = false;
                    for (int j = 0; j < _dirs.Length; j++)
                    {
                        Vector2 cc = c + _dirs[j];
                        if (EsCaminable(cc) && !CeldaEnZonaBomba(cc, c) && !CeldaEnCalor(cc))
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
    // [ DETECCIÓN DE AURORA ]
    // ═════════════════════════════════════════════════════════════════════════

    private bool AuroraEnLinea(Vector2 yo)
    {
        if (Aurora == null) return false;
        return AuroraEnLineaDesde(yo, Snap(Aurora.position));
    }

    private bool AuroraEnLineaDesde(Vector2 desde, Vector2 posAurora)
    {
        if (Mathf.Abs(posAurora.y - desde.y) < 0.1f)
        {
            float dist = Mathf.Abs(posAurora.x - desde.x);
            if (dist > 0.1f && dist <= _radioExplosion)
                if (!MuroEntre(desde, posAurora, posAurora.x > desde.x ? Vector2.right : Vector2.left))
                    return true;
        }
        if (Mathf.Abs(posAurora.x - desde.x) < 0.1f)
        {
            float dist = Mathf.Abs(posAurora.y - desde.y);
            if (dist > 0.1f && dist <= _radioExplosion)
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
        Vector2 posA = Snap(Aurora.position);

        for (int dirIdx = 0; dirIdx < _dirs.Length; dirIdx++)
        {
            Vector2 dir = _dirs[dirIdx];
            for (int i = 1; i <= _radioExplosion; i++)
            {
                Vector2 c = yo + dir * i;
                if (EsIndestructible(c)) break;
                if (!EsDestructible(c)) continue;
                if (posA == c + dir) return true;
                break;
            }
        }
        return false;
    }

    private bool DestructibleHaciaAurora(Vector2 yo)
    {
        if (Aurora == null) return false;
        Vector2 a = Snap(Aurora.position);
        Vector2 toA = (a - yo).normalized;

        for (int i = 0; i < _dirs.Length; i++)
        {
            Vector2 v = yo + _dirs[i];
            if (!EsDestructible(v)) continue;
            if (Vector2.Dot(_dirs[i], toA) > 0.5f) return true;
        }
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ ANÁLISIS / POWERUPS / CALOR ]
    // ═════════════════════════════════════════════════════════════════════════

    private int ContarSalidas(Vector2 celda)
    {
        int n = 0;
        for (int i = 0; i < _dirs.Length; i++)
        {
            Vector2 c = celda + _dirs[i];
            if (EsCaminable(c) && !HayPeligro(c)) n++;
        }
        return n;
    }

    private float ManhattanDist(Vector2 a, Vector2 b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private GameObject PowerupSeguro(float radio)
    {
        GameObject mejor = null;
        float min = Mathf.Infinity;
        var hits = Physics2D.OverlapCircleAll(transform.position, radio);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.CompareTag("Powerup")) continue;
            Vector2 cp = Snap(h.transform.position);
            if (HayPeligro(cp) || PeligroInminente(cp) || CeldaEnCalor(cp)) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < min) { min = d; mejor = h.gameObject; }
        }
        return mejor;
    }

    private void RegistrarCalor(Vector2 centro, float duracion = -1f)
    {
        if (duracion < 0f) duracion = CalorDuracion;
        float expira = Time.time + duracion;

        memoriaCalor[centro] = expira;
        for (int dirIdx = 0; dirIdx < _dirs.Length; dirIdx++)
        {
            Vector2 dir = _dirs[dirIdx];
            for (int i = 1; i <= _radioExplosion; i++)
            {
                Vector2 c = centro + dir * i;
                if (EsIndestructible(c)) break;
                if (!memoriaCalor.TryGetValue(c, out float prev) || expira > prev)
                    memoriaCalor[c] = expira;
                if (EsDestructible(c)) break;
            }
        }
    }

    private bool CeldaEnCalor(Vector2 c) =>
        memoriaCalor.TryGetValue(c, out float t) && Time.time < t;

    private void LimpiarCalor()
    {
        if (memoriaCalor.Count == 0) return;
        _bufferLimpiezaCalor.Clear();
        float tNow = Time.time;
        foreach (var kv in memoriaCalor)
            if (tNow >= kv.Value) _bufferLimpiezaCalor.Add(kv.Key);
        for (int i = 0; i < _bufferLimpiezaCalor.Count; i++)
            memoriaCalor.Remove(_bufferLimpiezaCalor[i]);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ VARIABILIDAD / DESATASCAR ]
    // ═════════════════════════════════════════════════════════════════════════

    private Vector2 CeldaAlternativa(Vector2 desde, Vector2 excluir)
    {
        Vector2[] dirsRand = DirsAleatorio();
        for (int i = 0; i < dirsRand.Length; i++)
        {
            Vector2 c = desde + dirsRand[i];
            if (c == excluir) continue;
            if (CeldaSegura(c) && EsCaminable(c)) return c;
        }
        return Vector2.zero;
    }

    private Vector2 CeldaLibreConMemoria(Vector2 desde)
    {
        Vector2 mejorCelda = Vector2.zero;
        float mejorScore = float.MinValue;
        Vector2[] dirsRand = DirsAleatorio();

        for (int i = 0; i < dirsRand.Length; i++)
        {
            Vector2 c = desde + dirsRand[i];
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

    private IEnumerator Desatascar(Vector2 desde)
    {
        Vector2[] dirsRand = DirsAleatorio();
        for (int i = 0; i < dirsRand.Length; i++)
        {
            Vector2 c = desde + dirsRand[i];
            if (EsCaminable(c) && CeldaSegura(c))
            { yield return StartCoroutine(MoverATile(c)); yield break; }
        }
        for (int i = 0; i < dirsRand.Length; i++)
        {
            Vector2 c = desde + dirsRand[i];
            if (EsCaminable(c))
            { yield return StartCoroutine(MoverATile(c)); yield break; }
        }
        yield return new WaitForSeconds(0.15f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // [ MOVIMIENTO ]
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

                if (ExplosionActivaEn(dest))
                {
                    abortarMovimiento = true;
                    rigidbody.position = Snap(rigidbody.position);
                    SetSprite(Vector2.zero);
                    yield break;
                }
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
    // [ SPRITES ]
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
    // [ DAÑO Y MUERTE ]
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

        OcultarTodosLosSprites();
        spriteRenderDamage.ResetAnimation();
        spriteRenderDamage.enabled = true;

        Invoke(nameof(TerminarDano), 2f);
    }

    private void TerminarDano()
    {
        OcultarTodosLosSprites();
        spriteRenderDamage.ResetAnimation();

        spriteRendererDown.enabled = true;
        activeSpriteRenderer = spriteRendererDown;
        activeSpriteRenderer.idle = true;

        if (controlBomba != null)
        {
            controlBomba.enabled = true;
            controlBomba.ResetPowerUps();
        }
        isInvincible = false;
        enRecuperacion = false;
        bombaPuesta = false;
        iaActiva = true;

        tiempoFinModoDefensivo = Time.time + modoDefensivoTrasDano;
        _corBuclePrincipal = StartCoroutine(BuclePrincipal());
    }

    private void IniciarMuerte()
    {
        isInvincible = true;
        iaActiva = false;

        StopAllCoroutines();
        CancelInvoke();
        if (controlBomba != null) controlBomba.enabled = false;

        OcultarTodosLosSprites();
        spriteRenderDeath.ResetAnimation();
        spriteRenderDeath.enabled = true;

        Invoke(nameof(FinMuerte), 3.25f);
    }

    private void FinMuerte() => gameObject.SetActive(false);

    // ═════════════════════════════════════════════════════════════════════════
    // [ UTILIDADES ]
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Vector2[] _dirs =
        { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    private static readonly Vector2Int[] _dirsInt =
        { new Vector2Int(0,1), new Vector2Int(0,-1),
          new Vector2Int(-1,0), new Vector2Int(1,0) };

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