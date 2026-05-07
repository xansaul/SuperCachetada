using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using System.Net;          // Necesario para Dns y IPAddress
using System.Net.Sockets;  // Necesario para AddressFamily

/// <summary>
/// Maneja la conexión inicial: permite al usuario crear una partida (Host)
/// o unirse a una existente introduciendo la IP del host.
/// 
/// Cuando se inicia como Host, detecta automáticamente la IP local de la
/// máquina y la muestra en pantalla para que pueda compartirla con el otro
/// jugador.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Referencias UI")]
    public Button botonHost;
    public Button botonCliente;
    public TMP_InputField campoIP;
    public TMP_Text textoEstado;

    [Header("Configuración de Red")]
    public ushort puerto = 7777;
    public string nombreEscenaBatalla = "Battle";
    public int jugadoresNecesarios = 2;

    void Start()
    {
        botonHost.onClick.AddListener(IniciarComoHost);
        botonCliente.onClick.AddListener(IniciarComoCliente);

        if (campoIP != null && string.IsNullOrEmpty(campoIP.text))
            campoIP.text = "127.0.0.1";

        textoEstado.text = "Selecciona una opción";

        NetworkManager.Singleton.OnClientConnectedCallback += AlConectarseUnCliente;
        NetworkManager.Singleton.OnClientDisconnectCallback += AlDesconectarseUnCliente;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= AlConectarseUnCliente;
            NetworkManager.Singleton.OnClientDisconnectCallback -= AlDesconectarseUnCliente;
        }
    }

    /// <summary>
    /// Inicia como HOST. Detecta automáticamente la IP local y la muestra
    /// para que el otro jugador la pueda usar.
    /// </summary>
    void IniciarComoHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        // Escuchar en TODAS las interfaces (0.0.0.0) para que clientes
        // en la red local puedan conectarse usando la IP real.
        transport.SetConnectionData("0.0.0.0", puerto);

        if (NetworkManager.Singleton.StartHost())
        {
            string ipLocal = ObtenerIPLocal();
            textoEstado.text = $"<b>Tu IP: {ipLocal}</b>\nComparte esta IP con tu oponente\nEsperando al jugador 2...";

            botonHost.interactable = false;
            botonCliente.interactable = false;
            campoIP.interactable = false;

            Debug.Log($"[LobbyManager] Host iniciado. IP local: {ipLocal}, puerto: {puerto}");
        }
        else
        {
            textoEstado.text = "Error al iniciar el Host";
        }
    }

    /// <summary>
    /// Inicia como CLIENTE conectándose a la IP que el usuario escribió.
    /// </summary>
    void IniciarComoCliente()
    {
        string ip = string.IsNullOrEmpty(campoIP.text) ? "127.0.0.1" : campoIP.text.Trim();

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, puerto);

        if (NetworkManager.Singleton.StartClient())
        {
            textoEstado.text = $"Conectando a {ip}...";
            botonHost.interactable = false;
            botonCliente.interactable = false;
            campoIP.interactable = false;
        }
        else
        {
            textoEstado.text = "Error al iniciar el cliente";
        }
    }

    void AlConectarseUnCliente(ulong clientId)
    {
        if (NetworkManager.Singleton.IsHost)
        {
            int conectados = NetworkManager.Singleton.ConnectedClientsIds.Count;

            if (conectados >= jugadoresNecesarios)
            {
                textoEstado.text = "¡Sala llena! Iniciando batalla...";
                NetworkManager.Singleton.SceneManager.LoadScene(
                    nombreEscenaBatalla,
                    LoadSceneMode.Single
                );
            }
            else
            {
                // Mantener visible la IP aunque cambie el conteo
                string ipLocal = ObtenerIPLocal();
                textoEstado.text = $"<b>Tu IP: {ipLocal}</b>\nJugadores conectados: {conectados}/{jugadoresNecesarios}";
            }
        }
        else
        {
            textoEstado.text = "Conectado. Esperando que comience la partida...";
        }
    }

    void AlDesconectarseUnCliente(ulong clientId)
    {
        textoEstado.text = "Un jugador se desconectó";
        botonHost.interactable = true;
        botonCliente.interactable = true;
        campoIP.interactable = true;
    }

    // ==========================================
    // AUTO-DETECCIÓN DE IP LOCAL
    // ==========================================

    /// <summary>
    /// Devuelve la IP local IPv4 de esta máquina en la red LAN.
    /// 
    /// Estrategia: abrir un socket UDP "ficticio" hacia una dirección externa
    /// (8.8.8.8, DNS de Google). Esto NO envía paquetes — solo le pide al
    /// sistema operativo que decida qué interfaz de red usaría para llegar
    /// a esa IP. La interfaz que elige es la que está conectada a la LAN
    /// activa, así que su IP local es la "buena".
    /// 
    /// Esta técnica es más confiable que Dns.GetHostEntry() en máquinas
    /// con múltiples adaptadores de red (VPN, máquinas virtuales, WiFi+
    /// Ethernet simultáneos, etc.) porque la elección la hace la tabla
    /// de enrutamiento del sistema, no nosotros adivinando.
    /// </summary>
    public static string ObtenerIPLocal()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                // Conectarse a una IP pública cualquiera (no se envía nada,
                // pero el OS decide qué adaptador usar para "llegar" allí).
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                if (endPoint != null)
                    return endPoint.Address.ToString();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LobbyManager] No se pudo detectar IP local con socket: {e.Message}");
        }

        // FALLBACK: si lo anterior falla (ej: sin internet, sin red),
        // usar Dns.GetHostEntry y buscar la primera IPv4 que no sea loopback.
        try
        {
            string hostName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
            foreach (IPAddress ip in hostEntry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LobbyManager] Fallback DNS también falló: {e.Message}");
        }

        // Último recurso: localhost (al menos funciona en la misma PC).
        return "127.0.0.1";
    }
}