using System.Text;
using Auditoria.Notificaciones.Mensajes;
using Auditoria.Notificaciones.Procesamiento;
using Microsoft.Extensions.Options;

namespace Auditoria.Notificaciones.Tests.Unitarias;

public class ProcesadorNotificacionesTests
{
    private readonly EnviadorEnMemoria _enviador = new();
    private readonly RegistroEnMemoria _registro = new();

    private ProcesadorNotificaciones CrearProcesador() => new(_registro, _enviador, Options.Create(new OpcionesNotificaciones()));

    [Fact]
    public async Task Evento_sensible_envia_un_correo()
    {
        var resultado = await CrearProcesador().ProcesarAsync(Eventos.Entrante(Eventos.Sensible()), CancellationToken.None);

        Assert.Equal(ResultadoProcesamiento.Enviada, resultado);
        Assert.Single(_enviador.Enviados);
    }

    [Fact]
    public async Task Evento_no_sensible_no_envia_ni_reserva()
    {
        var mensaje = Eventos.Entrante(Eventos.NoSensible());

        var resultado = await CrearProcesador().ProcesarAsync(mensaje, CancellationToken.None);

        Assert.Equal(ResultadoProcesamiento.NoSensible, resultado);
        Assert.Empty(_enviador.Enviados);
        Assert.False(_registro.EstaReservado(mensaje.MessageId));
    }

    [Fact]
    public async Task Mismo_message_id_entregado_dos_veces_envia_un_solo_correo()
    {
        var mensaje = Eventos.Entrante(Eventos.Sensible());
        var procesador = CrearProcesador();

        await procesador.ProcesarAsync(mensaje, CancellationToken.None);
        var segundo = await procesador.ProcesarAsync(mensaje, CancellationToken.None);

        Assert.Equal(ResultadoProcesamiento.Duplicada, segundo);
        Assert.Single(_enviador.Enviados);
    }

    [Fact]
    public async Task Si_el_smtp_falla_libera_la_reserva_para_que_el_reintento_envie()
    {
        var mensaje = Eventos.Entrante(Eventos.Sensible());
        var procesador = CrearProcesador();
        _enviador.ServidorCaido = true;

        await Assert.ThrowsAnyAsync<Exception>(() => procesador.ProcesarAsync(mensaje, CancellationToken.None));
        Assert.False(_registro.EstaReservado(mensaje.MessageId));

        _enviador.ServidorCaido = false;
        var reintento = await procesador.ProcesarAsync(mensaje, CancellationToken.None);

        Assert.Equal(ResultadoProcesamiento.Enviada, reintento);
        Assert.Single(_enviador.Enviados);
    }

    [Fact]
    public async Task Mensaje_ilegible_lanza_excepcion_para_terminar_en_la_dlq()
    {
        var mensaje = new MensajeEntrante(Guid.NewGuid(), Encoding.UTF8.GetBytes("{ no es json"), null);

        await Assert.ThrowsAnyAsync<Exception>(() => CrearProcesador().ProcesarAsync(mensaje, CancellationToken.None));
        Assert.Empty(_enviador.Enviados);
    }
}
