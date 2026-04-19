using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class HUDManager : MonoBehaviour
{
    [Header("Panel Victoria")]
    public GameObject panelVictoria;
    public Image imagenPanel;
    public TextMeshProUGUI txtNombre;
    public TextMeshProUGUI txtGano;
    public float tiempoFade = 1.5f;

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

    private bool juegoTerminado = false;

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

        if (aurora.Vidas <= 0)
        {
            FindObjectOfType<Temporizador>().DetenerTemporizador();
            juegoTerminado = true;
            txtNombre.text = "Lysara";
            Invoke(nameof(MostrarPanel), 3.5f);
        }
        else if (lysara.Vidas <= 0)
        {
            FindObjectOfType<Temporizador>().DetenerTemporizador();
            juegoTerminado = true;
            txtNombre.text = "Aurora";
            Invoke(nameof(MostrarPanel), 3.5f);
        }
    }
    private void Start()
    {
        FindObjectOfType<Temporizador>().onTiempoAgotado += VerificarGanadorPorTiempo;
    }

    private void VerificarGanadorPorTiempo()
    {
        if (juegoTerminado) return;

        juegoTerminado = true;

        if (aurora.Vidas > lysara.Vidas)
        {
            txtNombre.text = "Aurora";
            txtGano.text="GANA";
            Invoke(nameof(MostrarPanel), 0f);
        }
        else if (lysara.Vidas > aurora.Vidas)
        {
            txtNombre.text = "Lysara";
            txtGano.text="GANA";
            Invoke(nameof(MostrarPanel), 0f);
        }
        else
        {
            txtNombre.text = "¡EMPATE!";
            txtGano.text="";
            Invoke(nameof(MostrarPanel), 0f);
        }
    }

    private void MostrarPanel()
    {
        panelVictoria.SetActive(true);
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        Color colorPanel = imagenPanel.color;
        colorPanel.a = 0f;
        imagenPanel.color = colorPanel;

        txtNombre.alpha = 0f;
        txtGano.alpha = 0f;

        float tiempo = 0f;
        while (tiempo < tiempoFade)
        {
            tiempo += Time.deltaTime;
            float alpha = Mathf.Clamp01(tiempo / tiempoFade);

            colorPanel.a = alpha * 0.6f;
            imagenPanel.color = colorPanel;
            txtNombre.alpha = alpha;
            txtGano.alpha = alpha;

            yield return null;
        }
    }
}