using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// BattleManager versión multijugador con Netcode for GameObjects.
/// 
/// ARQUITECTURA:
/// - El HOST (jugador 1) es la autoridad: genera operaciones, valida
///   respuestas y calcula el daño.
/// - El CLIENTE (jugador 2) solo envía su respuesta y muestra los
///   resultados que recibe del host.
/// - Las variables importantes son NetworkVariable: se sincronizan 
///   automáticamente de host a cliente.
/// - submitAnswer del cliente se envía con ServerRpc.
/// - Animaciones, mensajes y operación nueva se notifican con ClientRpc.
/// - Al final del juego, el host envía un ClientRpc a todos para que
///   apaguen su conexión y vuelvan al menú.
/// - Si un jugador cierra el juego abruptamente, el otro detecta la
///   desconexión y vuelve al menú con un mensaje informativo.
/// </summary>
public class BattleManager : NetworkBehaviour
{
    // ==========================================
    // VARIABLES DEL INSPECTOR
    // ==========================================

    [Header("Conexiones de la UI")]
    public TMP_Text textoOperacion;
    public TMP_InputField entradaRespuesta;
    public TMP_Text textoVida;
    public TMP_Text textoTiempo;

    [Header("Barras de Vida")]
    public Slider barraVidaJ1;
    public Slider barraVidaJ2;

    [Header("Estadísticas")]
    public float vidaInicial = 100f;
    public float danoBase = 20f;
    public float tiempoMaximo = 10f;
    public float retrasoAlEmpezar = 2f;
    public float tiempoEsperaFinJuego = 5f;
    public float tiempoEsperaDesconexion = 3f;

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

    // ==========================================
    // NETWORK VARIABLES (sincronizadas automáticamente)
    // ==========================================

    private NetworkVariable<float> vidaJ1 = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> vidaJ2 = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> rondaActual = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // true = turno J1, false = turno J2
    private NetworkVariable<bool> esTurnoJugador1 = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> juegoTerminado = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ==========================================
    // VARIABLES INTERNAS DEL HOST
    // ==========================================
    private float tiempoActual;
    private int respuestaCorrecta;
    private bool esperandoSiguienteTurno = true;

    // ==========================================
    // INICIO Y FIN DEL CICLO DE VIDA EN RED
    // ==========================================

    public override void OnNetworkSpawn()
    {
        // Inicializar visualmente las barras (en ambos lados)
        if (barraVidaJ1 != null) { barraVidaJ1.maxValue = vidaInicial; barraVidaJ1.value = vidaInicial; }
        if (barraVidaJ2 != null) { barraVidaJ2.maxValue = vidaInicial; barraVidaJ2.value = vidaInicial; }

        if (renderizadorJ1 != null) renderizadorJ1.sprite = spriteJ1Quieto;
        if (renderizadorJ2 != null) renderizadorJ2.sprite = spriteJ2Quieto;

        textoOperacion.text = "¡PREPÁRATE!";
        if (textoTiempo != null) textoTiempo.text = "";

        // Suscribirse a cambios de las NetworkVariables para refrescar UI
        vidaJ1.OnValueChanged += (oldVal, newVal) => ActualizarUI();
        vidaJ2.OnValueChanged += (oldVal, newVal) => ActualizarUI();
        esTurnoJugador1.OnValueChanged += (oldVal, newVal) => ActualizarEstadoEntrada();
        juegoTerminado.OnValueChanged += (oldVal, newVal) => ActualizarEstadoEntrada();

        // Detectar desconexión del oponente
        NetworkManager.Singleton.OnClientDisconnectCallback += AlDesconectarseCliente;

        // Solo el host arranca el juego
        if (IsServer)
        {
            vidaJ1.Value = vidaInicial;
            vidaJ2.Value = vidaInicial;
            rondaActual.Value = 1;
            esTurnoJugador1.Value = true;
            juegoTerminado.Value = false;

            Invoke(nameof(GenerarOperacion), retrasoAlEmpezar);
        }

        ActualizarUI();
        ActualizarEstadoEntrada();
    }

    public override void OnNetworkDespawn()
    {
        // Desuscribirse del evento para evitar errores cuando se cambia de escena
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= AlDesconectarseCliente;
        }
    }

    // ==========================================
    // UPDATE — solo el host corre el timer
    // ==========================================

    void Update()
    {
        if (!IsServer) return;
        if (juegoTerminado.Value || esperandoSiguienteTurno) return;

        tiempoActual -= Time.deltaTime;

        MostrarTiempoClientRpc(tiempoActual);

        if (tiempoActual <= 0)
        {
            esperandoSiguienteTurno = true;
            MostrarMensajeClientRpc("¡Tiempo agotado! Pierde el turno.");
            PasarTurno();
        }
    }

    [ClientRpc]
    void MostrarTiempoClientRpc(float tiempo)
    {
        if (textoTiempo != null)
        {
            textoTiempo.text = Mathf.CeilToInt(tiempo).ToString();
            textoTiempo.color = (tiempo <= 3f) ? Color.red : Color.black;
        }
    }

    // ==========================================
    // GENERAR OPERACIÓN (solo host)
    // ==========================================

    public void GenerarOperacion()
    {
        if (!IsServer) return;
        if (juegoTerminado.Value) return;

        tiempoActual = tiempoMaximo;
        esperandoSiguienteTurno = false;

        int a = 0; int b = 0;
        string signoAritmetico = "";
        int tipoOperacion = Random.Range(0, 4);
        int ronda = rondaActual.Value;

        switch (tipoOperacion)
        {
            case 0:
                a = Random.Range(1, 5 + (ronda * 5));
                b = Random.Range(1, 5 + (ronda * 5));
                respuestaCorrecta = a + b;
                signoAritmetico = "+";
                break;
            case 1:
                a = Random.Range(5, 10 + (ronda * 5));
                b = Random.Range(1, a);
                respuestaCorrecta = a - b;
                signoAritmetico = "-";
                break;
            case 2:
                a = Random.Range(2, 3 + ronda);
                b = Random.Range(2, 3 + ronda);
                respuestaCorrecta = a * b;
                signoAritmetico = "x";
                break;
            case 3:
                int divisor = Random.Range(2, 3 + ronda);
                int cociente = Random.Range(2, 3 + ronda);
                int dividendo = divisor * cociente;
                a = dividendo;
                b = divisor;
                respuestaCorrecta = cociente;
                signoAritmetico = "÷";
                break;
        }

        MostrarOperacionClientRpc(a, b, signoAritmetico, rondaActual.Value, esTurnoJugador1.Value);
    }

    [ClientRpc]
    void MostrarOperacionClientRpc(int a, int b, string signo, int ronda, bool turnoJ1)
    {
        string nombreActual = turnoJ1 ? "Jugador 1" : "Jugador 2";
        textoOperacion.text = $"Ronda {ronda} - Turno de {nombreActual}\n{a} {signo} {b} = ?";

        ActualizarEstadoEntrada();

        if (EsMiTurno())
        {
            entradaRespuesta.text = "";
            entradaRespuesta.ActivateInputField();
        }
    }

    // ==========================================
    // RESPUESTA DEL JUGADOR
    // ==========================================

    public void ComprobarEntrada(string input)
    {
        if (juegoTerminado.Value) return;
        if (!EsMiTurno()) return;

        if (int.TryParse(input, out int respuestaNumerica))
        {
            EnviarRespuestaServerRpc(respuestaNumerica);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void EnviarRespuestaServerRpc(int valor, ServerRpcParams rpcParams = default)
    {
        if (juegoTerminado.Value || esperandoSiguienteTurno) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        bool senderEsHost = (senderId == NetworkManager.ServerClientId);
        bool senderEsTurno = (senderEsHost && esTurnoJugador1.Value) ||
                             (!senderEsHost && !esTurnoJugador1.Value);

        if (!senderEsTurno)
        {
            Debug.LogWarning($"[BattleManager] Cliente {senderId} envió respuesta fuera de turno. Ignorada.");
            return;
        }

        EvaluarRespuesta(valor == respuestaCorrecta);
    }

    void EvaluarRespuesta(bool esCorrecta)
    {
        if (!IsServer) return;

        esperandoSiguienteTurno = true;

        if (esCorrecta)
        {
            float multiplicador = tiempoActual / tiempoMaximo;
            float dano = danoBase * multiplicador;

            if (esTurnoJugador1.Value)
            {
                vidaJ2.Value = Mathf.Max(0, vidaJ2.Value - dano);
                ReproducirAtaqueClientRpc(true);
            }
            else
            {
                vidaJ1.Value = Mathf.Max(0, vidaJ1.Value - dano);
                ReproducirAtaqueClientRpc(false);
            }

            MostrarMensajeClientRpc("¡Correcto! Daño: " + Mathf.Round(dano));
        }
        else
        {
            MostrarMensajeClientRpc("¡Respuesta Incorrecta! Pierde el turno.");
        }

        RevisarFinDeJuego();
    }

    [ClientRpc]
    void MostrarMensajeClientRpc(string mensaje)
    {
        textoOperacion.text = mensaje;
    }

    [ClientRpc]
    void ReproducirAtaqueClientRpc(bool ataqueJ1)
    {
        if (fuenteAudio != null && sonidoSlap != null) fuenteAudio.PlayOneShot(sonidoSlap);

        if (ataqueJ1 && renderizadorJ1 != null)
            StartCoroutine(AnimarAtaque(renderizadorJ1, spriteJ1Ataque, spriteJ1Quieto, distanciaMovimiento));
        else if (!ataqueJ1 && renderizadorJ2 != null)
            StartCoroutine(AnimarAtaque(renderizadorJ2, spriteJ2Ataque, spriteJ2Quieto, -distanciaMovimientoJ2));
    }

    IEnumerator AnimarAtaque(SpriteRenderer renderizador, Sprite spriteAtq, Sprite spriteNrm, float direccion)
    {
        Transform t = renderizador.transform;
        Vector3 posOrig = t.position;
        Vector3 posObj = posOrig + new Vector3(direccion, 0, 0);

        renderizador.sprite = spriteAtq;

        float tiempo = 0;
        while (tiempo < 0.05f)
        {
            t.position = Vector3.Lerp(posOrig, posObj, tiempo / 0.05f);
            tiempo += Time.deltaTime;
            yield return null;
        }
        t.position = posObj;

        yield return new WaitForSeconds(duracionPose);

        renderizador.sprite = spriteNrm;

        tiempo = 0;
        while (tiempo < 0.15f)
        {
            t.position = Vector3.Lerp(posObj, posOrig, tiempo / 0.15f);
            tiempo += Time.deltaTime;
            yield return null;
        }
        t.position = posOrig;
    }

    // ==========================================
    // FIN DE TURNO Y FIN DE JUEGO (solo host)
    // ==========================================

    void RevisarFinDeJuego()
    {
        if (!IsServer) return;

        if (vidaJ1.Value <= 0 || vidaJ2.Value <= 0)
        {
            juegoTerminado.Value = true;

            string mensaje = (vidaJ1.Value <= 0)
                ? "¡El Jugador 2 es el Ganador!"
                : "¡El Jugador 1 es el Ganador!";
            MostrarFinJuegoClientRpc(mensaje);

            Invoke(nameof(NotificarRegresoAlMenu), tiempoEsperaFinJuego);
        }
        else
        {
            PasarTurno();
        }
    }

    [ClientRpc]
    void MostrarFinJuegoClientRpc(string mensaje)
    {
        textoOperacion.text = mensaje;
        if (entradaRespuesta != null) entradaRespuesta.gameObject.SetActive(false);
        if (textoTiempo != null) textoTiempo.gameObject.SetActive(false);
    }

    void NotificarRegresoAlMenu()
    {
        if (!IsServer) return;
        RegresarAlMenuClientRpc();
    }

    [ClientRpc]
    void RegresarAlMenuClientRpc()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene(0);
    }

    void PasarTurno()
    {
        if (!IsServer) return;

        esTurnoJugador1.Value = !esTurnoJugador1.Value;
        if (esTurnoJugador1.Value) rondaActual.Value++;

        Invoke(nameof(GenerarOperacion), 2f);
    }

    // ==========================================
    // MANEJO DE DESCONEXIÓN
    // ==========================================

    /// <summary>
    /// Se ejecuta cuando algún cliente se desconecta de la red.
    /// 
    /// CASOS POSIBLES:
    /// - Si soy el HOST y se desconectó el cliente: el oponente cerró el juego.
    /// - Si soy el CLIENTE y el host se desconectó: el host cerró el juego
    ///   (en este caso, clientId = 0 = el id del servidor).
    /// 
    /// En ambos escenarios, el jugador que se queda debe volver al menú con
    /// un mensaje informativo.
    /// </summary>
    void AlDesconectarseCliente(ulong clientId)
    {
        // Si el juego ya terminó normalmente, ignorar (el regreso al menú
        // ya está siendo manejado por RegresarAlMenuClientRpc).
        if (juegoTerminado.Value) return;

        Debug.Log($"[BattleManager] Cliente {clientId} se desconectó. Volviendo al menú.");

        if (textoOperacion != null)
            textoOperacion.text = "El oponente se desconectó.\nVolviendo al menú...";

        if (entradaRespuesta != null) entradaRespuesta.gameObject.SetActive(false);
        if (textoTiempo != null) textoTiempo.gameObject.SetActive(false);

        // Cancelar cualquier Invoke pendiente para evitar comportamiento extraño
        CancelInvoke();

        Invoke(nameof(ForzarRegresoAlMenu), tiempoEsperaDesconexion);
    }

    /// <summary>
    /// Apaga la red local y regresa al menú.
    /// No usa ClientRpc porque solo somos uno (el otro ya se desconectó).
    /// </summary>
    void ForzarRegresoAlMenu()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene(0);
    }

    // ==========================================
    // UI HELPERS
    // ==========================================

    void ActualizarUI()
    {
        float v1 = Mathf.Max(0, vidaJ1.Value);
        float v2 = Mathf.Max(0, vidaJ2.Value);

        if (textoVida != null)
            textoVida.text = "J1: " + Mathf.Round(v1) + " HP  |  J2: " + Mathf.Round(v2) + " HP";
        if (barraVidaJ1 != null) barraVidaJ1.value = v1;
        if (barraVidaJ2 != null) barraVidaJ2.value = v2;
    }

    bool EsMiTurno()
    {
        if (esTurnoJugador1.Value) return IsHost;
        else return !IsHost && IsClient;
    }

    void ActualizarEstadoEntrada()
    {
        if (entradaRespuesta == null) return;

        bool miTurno = EsMiTurno();
        entradaRespuesta.interactable = miTurno && !juegoTerminado.Value;
    }
}