using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;

namespace Retail.Infrastructure.IntegrationTests.TestData;

/// <summary>
/// Construye clientes válidos para pruebas. Desde que <see cref="Cliente"/> solo se crea con
/// <see cref="Cliente.Crear"/>, cada test necesita nombre y documento válidos aunque solo le interese la cuenta
/// corriente; este helper los completa y deja al test declarar únicamente lo que verifica.
/// </summary>
internal static class ClientesDePrueba
{
    private static int _secuencia = 10_000_000;

    public static Cliente Crear(
        int id = 0,
        string razonSocialONombre = "Librería del Centro S.R.L.",
        TipoDocumentoEnum tipoDocumento = TipoDocumentoEnum.Cuit,
        string? numeroDocumento = null,
        CondicionIvaEnum condicionIva = CondicionIvaEnum.ResponsableInscripto,
        bool tieneCuentaCorriente = false,
        decimal limiteCredito = 0m,
        decimal saldoCuentaCorriente = 0m,
        string? email = null)
    {
        var cliente = Cliente.Crear(
            razonSocialONombre,
            tipoDocumento,
            numeroDocumento ?? SiguienteCuit(),
            condicionIva,
            email: email);

        cliente.Id = id;
        cliente.TieneCuentaCorriente = tieneCuentaCorriente;
        cliente.LimiteCredito = limiteCredito;
        cliente.SaldoCuentaCorriente = saldoCuentaCorriente;
        return cliente;
    }

    /// <summary>
    /// Un CUIT de empresa distinto en cada llamada (los clientes de un mismo test no chocan en el índice único),
    /// con su dígito verificador módulo 11 correcto.
    /// </summary>
    public static string SiguienteCuit()
    {
        while (true)
        {
            var base10 = $"30{Interlocked.Increment(ref _secuencia):D8}";
            for (var digito = 0; digito <= 9; digito++)
            {
                var candidato = base10 + digito;
                if (ReglasDocumento.EsCuitValido(candidato))
                {
                    return candidato;
                }
            }
        }
    }
}
