using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement; // Requerido para cambiar de escenas

/// <summary>
/// Gestiona la lógica principal del combate, incluyendo turnos, generación de operaciones matemáticas,
/// cálculo de daño, animaciones y fin del juego.
/// </summary>
public class BattleManager : MonoBehaviour
{
    // ==========================================
    // VARIABLES DEL INSPECTOR
    // ==========================================

    [Header("Conexiones de la UI")]
    public TMP_Text textoOperacion;       // Muestra la ronda, turno y la operación matemática
    public TMP_InputField entradaRespuesta; // Caja donde el jugador escribe su respuesta
    public TMP_Text textoVida;            // Muestra la vida en formato numérico
    public TMP_Text textoTiempo;          // Reloj de cuenta regresiva

    [Header("Barras de Vida")]
    public Slider barraVidaJ1;            // Barra visual de salud del Jugador 1
    public Slider barraVidaJ2;            // Barra visual de salud del Jugador 2

    [Header("Estadísticas")]
    public float vidaJugador1 = 100f;
    public float vidaJugador2 = 100f;
    public float danoBase = 20f;          // Daño máximo posible si responden instantáneamente
    public float tiempoMaximo = 10f;      // Segundos permitidos por turno
    public float retrasoAlEmpezar = 2f;   // Segundos de espera antes de mostrar la primera suma
    public float tiempoEsperaFinJuego = 5f; // Segundos que el juego se pausa antes de ir al menú al ganar

    [Header("Animación de Ataque")]
    public float duracionPose = 0.3f;     // Tiempo que el personaje se queda con la mano estirada (pose de ataque)
    public float distanciaMovimiento = 1.5f;   // Cuánto se desplaza el J1 hacia la derecha
    public float distanciaMovimientoJ2 = 1.5f; // Cuánto se desplaza el J2 hacia la izquierda

    [Header("Sprites Jugador 1")]
    public SpriteRenderer renderizadorJ1; // Componente que dibuja al Jugador 1
    public Sprite spriteJ1Quieto;         // Imagen normal (Idle)
    public Sprite spriteJ1Ataque;         // Imagen atacando (Slap)

    [Header("Sprites Jugador 2")]
    public SpriteRenderer renderizadorJ2; // Componente que dibuja al Jugador 2
    public Sprite spriteJ2Quieto;         // Imagen normal (Idle)
    public Sprite spriteJ2Ataque;         // Imagen atacando (Slap)

    [Header("Sonidos")]
    public AudioSource fuenteAudio;       // Componente reproductor de sonido
    public AudioClip sonidoSlap;          // Archivo de audio de la cachetada

    // ==========================================
    // VARIABLES INTERNAS (ESTADO DEL JUEGO)
    // ==========================================
    private float tiempoActual;
    private bool esTurnoJugador1 = true;  // Indica de quién es el turno (true = J1, false = J2)
    private int respuestaCorrecta;        // Almacena el resultado de la operación actual
    private bool juegoTerminado = false;  // Bloquea el juego si alguien pierde toda la vida
    private bool esperandoSiguienteTurno = true; // Bloquea el tiempo y entradas durante animaciones/transiciones
    private int rondaActual = 1;          // Sube cada vez que ambos jugadores completan su turno

    // ==========================================
    // MÉTODOS PRINCIPALES
    // ==========================================

    void Start()
    {
        // Inicializar los límites visuales de las barras de vida
        if (barraVidaJ1 != null) { barraVidaJ1.maxValue = vidaJugador1; barraVidaJ1.value = vidaJugador1; }
        if (barraVidaJ2 != null) { barraVidaJ2.maxValue = vidaJugador2; barraVidaJ2.value = vidaJugador2; }

        // Asegurar que los personajes comiencen en su pose inactiva
        if (renderizadorJ1 != null) renderizadorJ1.sprite = spriteJ1Quieto;
        if (renderizadorJ2 != null) renderizadorJ2.sprite = spriteJ2Quieto;

        // Configurar la pantalla de espera inicial
        textoOperacion.text = "¡PREPÁRATE!";
        if (textoTiempo != null) textoTiempo.text = "";
        
        // Llamar a la primera operación después del retraso configurado
        Invoke("GenerarOperacion", retrasoAlEmpezar);
    }

    void Update()
    {
        // Detener el reloj si el juego terminó o si estamos en una pausa (animación/espera)
        if (juegoTerminado || esperandoSiguienteTurno) return;

        // Reducir el tiempo en función de los fotogramas del juego
        tiempoActual -= Time.deltaTime;
        
        // Actualizar el reloj visual
        if (textoTiempo != null)
        {
            // Mostrar número entero redondeado hacia arriba
            textoTiempo.text = Mathf.CeilToInt(tiempoActual).ToString();
            // Cambiar a color rojo como advertencia cuando queden 3 segundos o menos
            textoTiempo.color = (tiempoActual <= 3f) ? Color.red : Color.black;
        }

        // Detectar si el tiempo se agotó
        if (tiempoActual <= 0)
        {
            esperandoSiguienteTurno = true; // Bloquear nuevas entradas
            textoOperacion.text = "¡Tiempo agotado! Pierdes tu turno.";
            PasarTurno();
        }
    }

    /// <summary>
    /// Genera una operación matemática aleatoria basada en la ronda actual.
    /// La dificultad escala conforme avanza el juego.
    /// </summary>
    public void GenerarOperacion()
    {
        if (juegoTerminado) return; 

        tiempoActual = tiempoMaximo;     // Reiniciar el reloj
        esperandoSiguienteTurno = false; // Desbloquear el juego para que corra el Update
        
        int a = 0; int b = 0;
        string signoAritmetico = "";
        int tipoOperacion = Random.Range(0, 4); // 0=Suma, 1=Resta, 2=Multiplicación, 3=División

        // Lógica de escalado de dificultad basada en 'rondaActual'
        switch (tipoOperacion)
        {
            case 0: // Suma (Sube de 5 en 5 el límite máximo por ronda)
                a = Random.Range(1, 5 + (rondaActual * 5)); 
                b = Random.Range(1, 5 + (rondaActual * 5)); 
                respuestaCorrecta = a + b; 
                signoAritmetico = "+"; 
                break;
            case 1: // Resta ('a' siempre es mayor que 'b' para evitar resultados negativos)
                a = Random.Range(5, 10 + (rondaActual * 5)); 
                b = Random.Range(1, a); 
                respuestaCorrecta = a - b; 
                signoAritmetico = "-"; 
                break;
            case 2: // Multiplicación (Escala lentamente de 1 en 1 para mantener jugabilidad)
                a = Random.Range(2, 3 + rondaActual); 
                b = Random.Range(2, 3 + rondaActual); 
                respuestaCorrecta = a * b; 
                signoAritmetico = "x"; 
                break;
            case 3: // División Exacta (Se crea primero la solución y luego se multiplican las partes para garantizar divisibilidad)
                int divisor = Random.Range(2, 3 + rondaActual); 
                int cociente = Random.Range(2, 3 + rondaActual); 
                int dividendo = divisor * cociente; 
                a = dividendo; 
                b = divisor; 
                respuestaCorrecta = cociente; 
                signoAritmetico = "÷"; 
                break;
        }
        
        // Mostrar la información en pantalla
        string nombreActual = esTurnoJugador1 ? "Jugador 1" : "Jugador 2";
        textoOperacion.text = $"Ronda {rondaActual} - Turno de {nombreActual}\n{a} {signoAritmetico} {b} = ?";
        ActualizarUI();
        
        // Preparar la caja de texto para que el usuario escriba sin tener que darle clic
        entradaRespuesta.text = "";
        entradaRespuesta.ActivateInputField();
    }

    /// <summary>
    /// Se llama desde el InputField cuando el jugador presiona "Enter"
    /// </summary>
    /// <param name="input">El texto introducido por el usuario</param>
    public void ComprobarEntrada(string input)
    {
        if (juegoTerminado || esperandoSiguienteTurno) return;
        
        // Intentar convertir el texto a número. Si es válido, se evalúa.
        if (int.TryParse(input, out int respuestaNumerica))
        {
            EvaluarRespuesta(respuestaNumerica == respuestaCorrecta);
        }
    }

    /// <summary>
    /// Calcula el daño si la respuesta fue correcta y dispara animaciones/sonidos.
    /// </summary>
    void EvaluarRespuesta(bool esCorrecta)
    {
        esperandoSiguienteTurno = true; // Pausa el juego mientras vemos el resultado
        
        if (esCorrecta)
        {
            // Reproducir sonido de golpe
            if(fuenteAudio != null && sonidoSlap != null) fuenteAudio.PlayOneShot(sonidoSlap);
            
            // Cálculo de daño: Responder al instante hace daño máximo (20). 
            // Responder a los 5s (mitad del tiempo) hace 10 de daño.
            float multiplicador = tiempoActual / tiempoMaximo;
            float dano = danoBase * multiplicador;
            
            // Aplicar daño y lanzar animación al jugador correspondiente
            if (esTurnoJugador1)
            {
                vidaJugador2 -= dano;
                if (renderizadorJ1 != null) StartCoroutine(AnimarAtaque(renderizadorJ1, spriteJ1Ataque, spriteJ1Quieto, distanciaMovimiento));
            }
            else
            {
                vidaJugador1 -= dano;
                // El J2 usa distancia negativa (-distanciaMovimientoJ2) para moverse hacia la izquierda
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

    /// <summary>
    /// Corrutina para animar el salto del personaje hacia adelante, cambiar su imagen y devolverlo.
    /// </summary>
    IEnumerator AnimarAtaque(SpriteRenderer renderizador, Sprite spriteAtq, Sprite spriteNrm, float direccion)
    {
        Transform t = renderizador.transform;
        Vector3 posOrig = t.position;
        Vector3 posObj = posOrig + new Vector3(direccion, 0, 0); // Calcula a dónde va a saltar

        renderizador.sprite = spriteAtq; // Cambia a imagen de golpe
        
        // Movimiento de Ida (muy rápido: 0.05 segundos) usando Interpolación Lineal (Lerp)
        float tiempo = 0;
        while (tiempo < 0.05f) { 
            t.position = Vector3.Lerp(posOrig, posObj, tiempo / 0.05f); 
            tiempo += Time.deltaTime; 
            yield return null; 
        }
        t.position = posObj; // Asegura precisión al terminar la ida

        // Mantiene la pose de ataque un instante
        yield return new WaitForSeconds(duracionPose);

        renderizador.sprite = spriteNrm; // Regresa a imagen de inactividad
        
        // Movimiento de Vuelta (un poco más lento: 0.15 segundos)
        tiempo = 0;
        while (tiempo < 0.15f) { 
            t.position = Vector3.Lerp(posObj, posOrig, tiempo / 0.15f); 
            tiempo += Time.deltaTime; 
            yield return null; 
        }
        t.position = posOrig; // Asegura precisión al terminar la vuelta
    }

    /// <summary>
    /// Comprueba las barras de vida para determinar si la batalla ha concluido.
    /// </summary>
    void RevisarFinDeJuego()
    {
        if (vidaJugador1 <= 0 || vidaJugador2 <= 0)
        {
            juegoTerminado = true;
            entradaRespuesta.gameObject.SetActive(false); // Esconde la caja de respuesta
            
            // Declarar al ganador
            if (vidaJugador1 <= 0) textoOperacion.text = "¡El Jugador 2 es el Ganador!";
            else textoOperacion.text = "¡El Jugador 1 es el Ganador!";
            
            if (textoTiempo != null) textoTiempo.gameObject.SetActive(false); // Esconde el reloj

            // Esperar el tiempo parametrizable antes de regresar al Menú Principal
            Invoke("RegresarAlMenu", tiempoEsperaFinJuego);
        }
        else 
        { 
            PasarTurno(); 
        }
    }

    /// <summary>
    /// Función auxiliar para cargar la escena 0 (Menú)
    /// </summary>
    void RegresarAlMenu()
    {
        SceneManager.LoadScene(0);
    }

    /// <summary>
    /// Cambia los booleanos de turno y programa la siguiente ronda
    /// </summary>
    void PasarTurno()
    {
        esTurnoJugador1 = !esTurnoJugador1; // Invierte el booleano
        
        // Si vuelve a ser el turno del J1, significa que terminó una ronda completa
        if (esTurnoJugador1) rondaActual++; 
        
        // Dar tiempo a los jugadores para leer el resultado antes de la siguiente pregunta
        Invoke("GenerarOperacion", 2f); 
    }

    /// <summary>
    /// Sincroniza los valores numéricos con los elementos visuales de la interfaz
    /// </summary>
    void ActualizarUI()
    {
        // Mathf.Max(0, valor) asegura que visualmente la vida no muestre números negativos
        float v1 = Mathf.Max(0, vidaJugador1); 
        float v2 = Mathf.Max(0, vidaJugador2);
        
        textoVida.text = "J1: " + Mathf.Round(v1) + " HP  |  J2: " + Mathf.Round(v2) + " HP";
        
        if (barraVidaJ1 != null) barraVidaJ1.value = v1;
        if (barraVidaJ2 != null) barraVidaJ2.value = v2;
    }
}