using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class Temporizador : MonoBehaviour
{
    public TextMeshProUGUI txtTiempo;
    public float tiempoTotal_seg = 180f; // 3 minutos
    private float tiempoRestante;
    private bool corriendo = true;
    public System.Action onTiempoAgotado;

    private void Start()
    {
        tiempoRestante = tiempoTotal_seg;
    }

    private void Update()
    {
        if (!corriendo) return;

        tiempoRestante -= Time.deltaTime;

        if (tiempoRestante <= 0)
        {
            tiempoRestante = 0;
            corriendo = false;
            TiempoAgotado();
        }

        MostrarTiempo(tiempoRestante);
    }

    private void MostrarTiempo(float tiempo)
    {
        int minutos = Mathf.FloorToInt(tiempo / 60);
        int segundos = Mathf.FloorToInt(tiempo % 60);
        txtTiempo.text = string.Format("{0}:{1:00}", minutos, segundos);
    }

    private void TiempoAgotado()
    {
        corriendo = false;
        onTiempoAgotado?.Invoke();
    }

    public void DetenerTemporizador()
    {
        corriendo = false;
    }

    public void ReanudarTemporizador()
    {
        corriendo = true;
    }


}