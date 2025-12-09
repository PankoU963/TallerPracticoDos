using System;
using UnityEngine;

/// <summary>
/// Componente simple para que el jugador almacene el objeto que tiene en la mano.
/// El sistema de pickup/grab debe llamar a `Pickup` y `Drop` según corresponda.
/// ProyectorSlot consultará este componente para colocar directamente el objeto que el jugador tenga.
/// </summary>
public class PlayerPickupInventory : MonoBehaviour
{
    public PlaceableSculpture Held { get; private set; }

    /// <summary>
    /// Llamar al recoger el objeto.
    /// No modifica la jerarquía ni habilita/deshabilita colliders: eso lo debe hacer el sistema de pickup.
    /// </summary>
    public void Pickup(PlaceableSculpture sculpture)
    {
        Held = sculpture;
    }

    /// <summary>
    /// Dejar/soltar el objeto que se tenía en la mano.
    /// Devuelve la escultura que estaba sostenida (o null).
    /// </summary>
    public PlaceableSculpture Drop()
    {
        var tmp = Held;
        Held = null;
        return tmp;
    }

    public bool HasHeld() => Held != null;

    public void ClearHeld() => Held = null;
}
