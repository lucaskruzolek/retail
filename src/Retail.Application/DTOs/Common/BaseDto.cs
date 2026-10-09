namespace Retail.Application.DTOs.Common;

using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// Registro base para DTOs consumidos por capas de presentación (UI / WPF).
/// Implementa <see cref="INotifyPropertyChanged"/> para prevenir retención de memoria (binding leaks)
/// en el motor de enlaces de WPF ocasionados por suscripciones en TypeDescriptor.AddValueChanged.
/// </summary>
public abstract record class BaseDto : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Notifica el cambio de una propiedad si el DTO requiere reactividad o mutabilidad controlada.
    /// </summary>
    /// <param name="propertyName">Nombre de la propiedad modificada.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Igualdad de la base sin el evento <see cref="PropertyChanged"/>. Un <c>record</c> compara todos sus campos, y
    /// el campo oculto del evento guarda sus suscriptores: cada <c>Binding</c> de WPF que se suscribía cambiaba el
    /// <see cref="GetHashCode"/> del DTO, y los <c>Selector</c> (ListBox, DataGrid), que guardan la selección en un
    /// diccionario por hash, quedaban corruptos. Los records derivados siguen comparando sus propiedades.
    /// </summary>
    public virtual bool Equals(BaseDto? other)
    {
        return other is not null && EqualityContract == other.EqualityContract;
    }

    public override int GetHashCode()
    {
        return EqualityContract.GetHashCode();
    }
}
