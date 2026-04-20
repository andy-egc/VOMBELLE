using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class HUDManager : MonoBehaviour
{
    [Header("Panel de resultado")]
    public GameObject panelVictoria;
    public GameObject panelDerrota;
    public GameObject panelEmpate;
    public float tiempoFade = 1.5f;

    [Header("Componentes Panel Victoria")]
    public Image imagenPanelVictoria;
    public TextMeshProUGUI txtVictoria;
    public TextMeshProUGUI txtNombreV;

    [Header("Componentes Panel Derrota")]
    public Image imagenPanelDerrota;
    public TextMeshProUGUI txtDerrota;
    public TextMeshProUGUI txtNombreD;

    [Header("Componentes Panel Empate")]
    public TextMeshProUGUI txtEmpate;

    [Header("Audio")]
    public AudioSource audioCamera;
    public AudioSource audioPanelVictoria;
    public AudioSource audioPanelDerrota;

    [Header("Referencias Personajes")]
    public ControlMovimiento aurora;
    public ControlMovimientoLysara lysara;
    public ControlBomba bombaAurora;
    public ControlBomba bombaLysara;

    [Header("Textos Aurora")]
    public TextMeshProUGUI txtVidasAurora;
    public TextMeshProUGUI txtCantidadBombasAurora;
    public TextMeshProUGUI txtRangoExplosionAurora;
    public TextMeshProUGUI txtVelocidadAurora;

    [Header("Textos Lysara")]
    public TextMeshProUGUI txtVidasLysara;
    public TextMeshProUGUI txtCantidadBombasLysara;
    public TextMeshProUGUI txtRangoExplosionLysara;
    public TextMeshProUGUI txtVelocidadLysara;

    private enum Resultado { Victoria, Derrota, Empate }
    private bool juegoTerminado = false;

    private void Start()
    {
        FindObjectOfType<Temporizador>().onTiempoAgotado += VerificarGanadorPorTiempo;
    }

    private void Update()
    {
        if (juegoTerminado) return;

        txtVidasAurora.text = "x" + aurora.Vidas;
        txtCantidadBombasAurora.text = "x" + bombaAurora.bombAmount;
        txtRangoExplosionAurora.text = "x" + bombaAurora.explosionRadius;
        txtVelocidadAurora.text = "x" + aurora.speed;

        txtVidasLysara.text = "x" + lysara.Vidas;
        txtCantidadBombasLysara.text = "x" + bombaLysara.bombAmount;
        txtRangoExplosionLysara.text = "x" + bombaLysara.explosionRadius;
        txtVelocidadLysara.text = "x" + lysara.speed;

        // Aurora muere → Lysara gana (victoria de Lysara = derrota de Aurora)
        if (aurora.Vidas <= 0 && !juegoTerminado)
        {
            TerminarJuego(Resultado.Derrota, 3.5f);
        }
        // Lysara muere → Aurora gana
        else if (lysara.Vidas <= 0 && !juegoTerminado)
        {
            TerminarJuego(Resultado.Victoria, 3.5f);
        }
    }

    private void VerificarGanadorPorTiempo()
    {
        if (juegoTerminado) return;

        if (aurora.Vidas > lysara.Vidas)
            TerminarJuego(Resultado.Victoria, 0f);
        else if (lysara.Vidas > aurora.Vidas)
            TerminarJuego(Resultado.Derrota, 0f);
        else
            TerminarJuego(Resultado.Empate, 0f);
    }

    private void TerminarJuego(Resultado resultado, float delay)
    {
        juegoTerminado = true;
        FindObjectOfType<Temporizador>().DetenerTemporizador();
        StartCoroutine(MostrarPanelConDelay(resultado, delay));
    }

    private IEnumerator MostrarPanelConDelay(Resultado resultado, float delay)
{
    if (delay > 0f) yield return new WaitForSeconds(delay);

    GameObject panel;
    Image imagen = null;
    TextMeshProUGUI txtResultado = null, txtNombre = null;

    AudioSource audioPanel;
    if (resultado == Resultado.Victoria)
        audioPanel = audioPanelVictoria;
    else if (resultado == Resultado.Derrota)
        audioPanel = audioPanelDerrota;
    else
        audioPanel = null; // Empate: la música de la cámara sigue sonando

    switch (resultado)
    {
        case Resultado.Victoria:
            panel        = panelVictoria;
            imagen       = imagenPanelVictoria;
            txtResultado = txtVictoria;
            txtNombre    = txtNombreV;
            break;
        case Resultado.Derrota:
            panel        = panelDerrota;
            imagen       = imagenPanelDerrota;
            txtResultado = txtDerrota;
            txtNombre    = txtNombreD;
            break;
        default: // Empate
            panel        = panelEmpate;
            txtResultado = txtEmpate;
            break;
    }

    // Solo pausar cámara y reproducir panel si NO es empate
    if (audioPanel != null)
    {
        if (audioCamera != null) audioCamera.Pause();
        audioPanel.Play();
    }

    panel.SetActive(true);
    yield return StartCoroutine(FadeIn(imagen, txtResultado, txtNombre));
}

    private IEnumerator FadeIn(Image imagen, TextMeshProUGUI txtResultado, TextMeshProUGUI txtNombre)
    {
        if (imagen != null)
        {
            Color colorPanel = imagen.color;
            colorPanel.a = 0f;
            imagen.color = colorPanel;
        }

        if (txtResultado != null) txtResultado.alpha = 0f;
        if (txtNombre != null) txtNombre.alpha = 0f;

        float tiempo = 0f;
        while (tiempo < tiempoFade)
        {
            tiempo += Time.deltaTime;
            float alpha = Mathf.Clamp01(tiempo / tiempoFade);

            if (imagen != null)
            {
                Color colorPanel = imagen.color;
                colorPanel.a = alpha * 0.6f;
                imagen.color = colorPanel;
            }

            if (txtResultado != null) txtResultado.alpha = alpha;
            if (txtNombre != null) txtNombre.alpha = alpha;

            yield return null;
        }
    }
}