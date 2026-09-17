namespace Auditoria.Domain.Comun;

public interface IEventoDominio
{
    DateTimeOffset OcurridoEn { get; }
}

public interface ITieneEventosDominio
{
    IReadOnlyCollection<IEventoDominio> EventosDominio { get; }
    void LimpiarEventosDominio();
}
