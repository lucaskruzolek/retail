using System.Diagnostics.CodeAnalysis;
using Retail.Application.DTOs.Sesiones;
using Retail.Application.DTOs.Usuarios;

namespace Retail.App.ViewModels.Usuarios;

/// <summary>
/// Fila de la grilla de Usuarios para un Gerente: el operador más "dónde está conectado" (MT-3). Es un
/// <see cref="UsuarioDto"/> con dos datos de presentación, así que la grilla y los comandos existentes siguen
/// funcionando con el mismo tipo base.
/// </summary>
public record class UsuarioConSesion : UsuarioDto
{
    [SetsRequiredMembers]
    public UsuarioConSesion(UsuarioDto operador, SesionActivaDto? sesion, bool esOtroUsuario)
        : base(operador)
    {
        TieneSesion = sesion is not null;
        PuedeCerrarSesion = sesion is not null && esOtroUsuario;
        ConectadoEn = Describir(sesion);
    }

    /// <summary>Verdadero si el operador tiene una sesión abierta (vigente o sin señal).</summary>
    public bool TieneSesion { get; }

    /// <summary>Se ofrece "Cerrar sesión" solo si hay sesión y no es la propia cuenta del Gerente.</summary>
    public bool PuedeCerrarSesion { get; }

    /// <summary>Texto de la columna "Conectado en"; vacío si no hay sesión.</summary>
    public string ConectadoEn { get; }

    private static string Describir(SesionActivaDto? sesion)
    {
        if (sesion is null)
        {
            return string.Empty;
        }

        return sesion.Vencida
            ? $"{sesion.NombreTerminal} · Sin señal desde {Retail.App.Services.ConversionHora.AHoraLocal(sesion.UltimoLatidoUtc):HH:mm}"
            : $"{sesion.NombreTerminal} · desde {Retail.App.Services.ConversionHora.AHoraLocal(sesion.InicioUtc):HH:mm}";
    }
}
