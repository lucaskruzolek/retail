namespace Retail.Domain.Exceptions;

/// <summary>
/// Se intentó hacer una copia de seguridad o una restauración desde una terminal que no es la PC servidor (MT-12). Es una
/// <see cref="DomainException"/>: la interfaz y <c>Retail.Cli</c> ya muestran su mensaje sin el detalle técnico.
/// </summary>
public class OperacionSoloEnServidorException : DomainException
{
    public OperacionSoloEnServidorException(string nombreEquipoServidor)
        : base($"Las copias de seguridad y la restauración se hacen en la PC servidor («{nombreEquipoServidor}»). " +
               "Desde esta terminal solo puede consultar el historial.")
    {
    }
}
