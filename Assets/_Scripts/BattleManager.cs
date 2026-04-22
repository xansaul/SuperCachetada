using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement; // NUEVO: Requerido para cambiar de escenas

public class BattleManager : MonoBehaviour
{
    [Header("Conexiones de la UI")]
    public TMP_Text textoOperacion;
    public TMP_InputField entradaRespuesta;
    public TMP_Text textoVida;
    public TMP_Text textoTiempo;

    [Header("Barras de Vida")]
    public Slider barraVidaJ1;
    public Slider barraVidaJ2;

    [Header("Estadísticas")]
    public float vidaJugador1 = 100f;
    public float vidaJugador2 = 100f;
    public float danoBase = 20f;
    public float tiempoMaximo = 10f;
    public float retrasoAlEmpezar = 2f; 
    public float tiempoEsperaFinJuego = 5f; // NUEVO: Tiempo parametrizable al ganar

    [Header("Animación de Ataque")]
    public float duracionPose = 0.3f; 
    public float distanciaMovimiento = 1.5f;
    public float distanciaMovimientoJ2 = 1.5f;

    [Header("Sprites Jugador 1")]
    public SpriteRenderer renderizadorJ1;
    public Sprite spriteJ1Quieto;
    public Sprite spriteJ1Ataque;

    [Header("Sprites Jugador 2")]
    public SpriteRenderer renderizadorJ2;
    public Sprite spriteJ2Quieto;
    public Sprite spriteJ2Ataque;

    [Header("Sonidos")]
    public AudioSource fuenteAudio;
    public AudioClip sonidoSlap;

    private float tiempoActual;
    private bool esTurnoJugador1 = true; 
    private int respuestaCorrecta;
    
    private bool juegoTerminado = false; 
    private bool esperandoSiguienteTurno = true; 
    private int rondaActual = 1;

    void Start()
    {
        if (barraVidaJ1 != null) { barraVidaJ1.maxValue = vidaJugador1; barraVidaJ1.value = vidaJugador1; }
        if (barraVidaJ2 != null) { barraVidaJ2.maxValue = vidaJugador2; barraVidaJ2.value = vidaJugador2; }

        if (renderizadorJ1 != null) renderizadorJ1.sprite = spriteJ1Quieto;
        if (renderizadorJ2 != null) renderizadorJ2.sprite = spriteJ2Quieto;

        textoOperacion.text = "¡PREPÁRATE!";
        if (textoTiempo != null) textoTiempo.text = "";
        
        Invoke("GenerarOperacion", retrasoAlEmpezar);
    }

    void Update()
    {
        if (juegoTerminado || esperandoSiguienteTurno) return;

        tiempoActual -= Time.deltaTime;
        
        if (textoTiempo != null)
        {
            textoTiempo.text = Mathf.CeilToInt(tiempoActual).ToString();
            textoTiempo.color = (tiempoActual <= 3f) ? Color.red : Color.black;
        }

        if (tiempoActual <= 0)
        {
            esperandoSiguienteTurno = true;
            textoOperacion.text = "¡Tiempo agotado! Pierdes tu turno.";
            PasarTurno();
        }
    }

    public void GenerarOperacion()
    {
        if (juegoTerminado) return; 

        tiempoActual = tiempoMaximo;
        esperandoSiguienteTurno = false; 
        
        int a = 0; int b = 0;
        string signoAritmetico = "";
        int tipoOperacion = Random.Range(0, 4);

        switch (tipoOperacion)
        {
            case 0: a = Random.Range(1, 5 + (rondaActual * 5)); b = Random.Range(1, 5 + (rondaActual * 5)); respuestaCorrecta = a + b; signoAritmetico = "+"; break;
            case 1: a = Random.Range(5, 10 + (rondaActual * 5)); b = Random.Range(1, a); respuestaCorrecta = a - b; signoAritmetico = "-"; break;
            case 2: a = Random.Range(2, 3 + rondaActual); b = Random.Range(2, 3 + rondaActual); respuestaCorrecta = a * b; signoAritmetico = "x"; break;
            case 3: int divisor = Random.Range(2, 3 + rondaActual); int cociente = Random.Range(2, 3 + rondaActual); int dividendo = divisor * cociente; a = dividendo; b = divisor; respuestaCorrecta = cociente; signoAritmetico = "÷"; break;
        }
        
        string nombreActual = esTurnoJugador1 ? "Jugador 1" : "Jugador 2";
        textoOperacion.text = $"Ronda {rondaActual} - Turno de {nombreActual}\n{a} {signoAritmetico} {b} = ?";
        ActualizarUI();
        
        entradaRespuesta.text = "";
        entradaRespuesta.ActivateInputField();
    }

    public void ComprobarEntrada(string input)
    {
        if (juegoTerminado || esperandoSiguienteTurno) return;
        if (int.TryParse(input, out int respuestaNumerica))
        {
            EvaluarRespuesta(respuestaNumerica == respuestaCorrecta);
        }
    }

    void EvaluarRespuesta(bool esCorrecta)
    {
        esperandoSiguienteTurno = true; 
        
        if (esCorrecta)
        {
            if(fuenteAudio != null && sonidoSlap != null) fuenteAudio.PlayOneShot(sonidoSlap);
            
            float multiplicador = tiempoActual / tiempoMaximo;
            float dano = danoBase * multiplicador;
            
            if (esTurnoJugador1)
            {
                vidaJugador2 -= dano;
                if (renderizadorJ1 != null) StartCoroutine(AnimarAtaque(renderizadorJ1, spriteJ1Ataque, spriteJ1Quieto, distanciaMovimiento));
            }
            else
            {
                vidaJugador1 -= dano;
                if (renderizadorJ2 != null) StartCoroutine(AnimarAtaque(renderizadorJ2, spriteJ2Ataque, spriteJ2Quieto, -distanciaMovimientoJ2));
            }
            textoOperacion.text = "¡Correcto! Daño: " + Mathf.Round(dano); 
        }
        else
        {
            textoOperacion.text = "¡Respuesta Incorrecta! Pierdes el turno.";
        }
        
        ActualizarUI();
        RevisarFinDeJuego(); 
    }

    IEnumerator AnimarAtaque(SpriteRenderer renderizador, Sprite spriteAtq, Sprite spriteNrm, float direccion)
    {
        Transform t = renderizador.transform;
        Vector3 posOrig = t.position;
        Vector3 posObj = posOrig + new Vector3(direccion, 0, 0);

        renderizador.sprite = spriteAtq;
        float tiempo = 0;
        while (tiempo < 0.05f) { t.position = Vector3.Lerp(posOrig, posObj, tiempo / 0.05f); tiempo += Time.deltaTime; yield return null; }
        t.position = posObj;

        yield return new WaitForSeconds(duracionPose);

        renderizador.sprite = spriteNrm;
        tiempo = 0;
        while (tiempo < 0.15f) { t.position = Vector3.Lerp(posObj, posOrig, tiempo / 0.15f); tiempo += Time.deltaTime; yield return null; }
        t.position = posOrig;
    }

    void RevisarFinDeJuego()
    {
        if (vidaJugador1 <= 0 || vidaJugador2 <= 0)
        {
            juegoTerminado = true;
            entradaRespuesta.gameObject.SetActive(false); 
            if (vidaJugador1 <= 0) textoOperacion.text = "¡El Jugador 2 es el Ganador!";
            else textoOperacion.text = "¡El Jugador 1 es el Ganador!";
            if (textoTiempo != null) textoTiempo.gameObject.SetActive(false);

            // NUEVO: Llamamos a la función de regresar al menú después de 'tiempoEsperaFinJuego' segundos
            Invoke("RegresarAlMenu", tiempoEsperaFinJuego);
        }
        else { PasarTurno(); }
    }

    // NUEVO: Función que carga la escena 0
    void RegresarAlMenu()
    {
        SceneManager.LoadScene(0);
    }

    void PasarTurno()
    {
        esTurnoJugador1 = !esTurnoJugador1;
        if (esTurnoJugador1) rondaActual++;
        Invoke("GenerarOperacion", 2f); 
    }

    void ActualizarUI()
    {
        float v1 = Mathf.Max(0, vidaJugador1); float v2 = Mathf.Max(0, vidaJugador2);
        textoVida.text = "J1: " + Mathf.Round(v1) + " HP  |  J2: " + Mathf.Round(v2) + " HP";
        if (barraVidaJ1 != null) barraVidaJ1.value = v1;
        if (barraVidaJ2 != null) barraVidaJ2.value = v2;
    }
}