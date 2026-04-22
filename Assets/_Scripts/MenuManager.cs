using UnityEngine;
using UnityEngine.SceneManagement; // NUEVO: Súper importante para cambiar escenas

public class MenuManager : MonoBehaviour
{
    // Función para el botón JUGAR
    public void EmpezarJuego()
    {
        // El número 1 es el índice de tu escena de juego en el Build Settings
        SceneManager.LoadScene(1); 
    }

    // Función para el botón SALIR
    public void SalirDelJuego()
    {
        Debug.Log("El juego se ha cerrado"); // Esto es para que lo veas en la consola
        Application.Quit(); // Cierra el juego (nota: no se nota dentro del editor de Unity, solo cuando exportas el juego)
    }
}