# Super Cachetada

Un juego de combate por turnos para dos jugadores donde el arma principal es el cálculo mental. Resuelve operaciones matemáticas para abofetear a tu oponente y ganar el encuentro.

## Características
- **Dificultad Progresiva:** Las operaciones se vuelven más difíciles conforme avanzan las rondas.
- **Cuatro Operaciones:** Suma, resta, multiplicación y división exacta.
- **Feedback Visual y Sonoro:** - Cambio de sprites al atacar.
  - Animación de movimiento hacia adelante (anticipación de golpe).
  - Sonido de cachetada al acertar.
- **Sistema de Daño:** El daño es proporcional a la velocidad de respuesta.
- **Interfaz Pulida:** Barras de vida con marcos personalizados, temporizador con alerta visual y menús de navegación.

## Requisitos Técnicos
- **Motor:** Unity 2021.3+ (o superior).
- **Paquetes:** TextMeshPro.
- **Escenas:** - `0`: Menú Principal.
  - `1`: Escena de Batalla.

## Documentación del Código

### 1. BattleManager.cs
Es el "cerebro" del juego. Controla el flujo de la partida.

* **GenerarOperacion():** Utiliza un `switch` para elegir entre 4 tipos de operaciones. La dificultad escala usando la variable `rondaActual` para definir el rango de los números aleatorios.
* **EvaluarRespuesta(bool esCorrecta):** - Si es correcta: Calcula el daño basado en el tiempo restante, reproduce el sonido y activa la corrutina de animación.
    - Si es incorrecta: Muestra un mensaje y pasa el turno.
* **IEnumerator AnimarAtaque():** Una corrutina que desplaza el sprite del jugador hacia adelante usando `Vector3.Lerp`, cambia el sprite al de ataque, espera un tiempo de pose y regresa al jugador a su posición original.
* **RevisarFinDeJuego():** Comprueba si alguna barra de vida llegó a cero. Si ocurre, bloquea el juego y usa `Invoke` para regresar al menú principal tras 5 segundos.

### 2. MenuManager.cs
Un script ligero para la navegación.
* **EmpezarJuego():** Carga la escena de batalla (índice 1).
* **SalirDelJuego():** Cierra la aplicación (útil en versiones build).

## Cómo Configurar
1. Clona el repositorio.
2. Abre el proyecto en Unity.
3. Asegúrate de que las fuentes de **TextMeshPro** estén instaladas.
4. Configura el **Build Settings** asegurándote de que el Menú sea la escena 0 y el Juego la escena 1.
5. En el objeto `BattleManager` del Inspector, asigna los Sprites (Idle y Attack), los Sliders de vida, el AudioSource y los textos de TMP.

## Cómo Jugar
1. El juego inicia con un mensaje de "¡PREPÁRATE!".
2. En cada turno, lee la operación y escribe el resultado en el campo de texto.
3. Presiona `Enter` para confirmar.
4. Responde lo más rápido posible: entre más rápido lo hagas, ¡más fuerte será la cachetada!