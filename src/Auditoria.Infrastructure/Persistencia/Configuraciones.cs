using Auditoria.Domain.Registros;
using Auditoria.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auditoria.Infrastructure.Persistencia;

internal sealed class RegistroAuditoriaConfiguracion : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> b)
    {
        b.ToTable("registro_auditoria");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(r => r.CodigoEmpresa).HasColumnName("codigo_empresa").HasMaxLength(RegistroAuditoria.LongitudCodigoEmpresa);
        b.Property(r => r.CodigoModulo).HasColumnName("codigo_modulo").HasMaxLength(2)
            .HasConversion(v => v!.Valor, v => CodigoModulo.Desde(v).Valor);
        b.Property(r => r.CodigoTransaccion).HasColumnName("codigo_transaccion").HasMaxLength(RegistroAuditoria.LongitudCodigoTransaccion);
        b.Property(r => r.Usuario).HasColumnName("usuario").HasMaxLength(RegistroAuditoria.LongitudUsuario);
        b.Property(r => r.Entidad).HasColumnName("entidad").HasMaxLength(RegistroAuditoria.LongitudEntidad);
        b.Property(r => r.ClaveEntidad).HasColumnName("clave_entidad").HasMaxLength(RegistroAuditoria.LongitudClaveEntidad);
        b.Property(r => r.Accion).HasColumnName("accion").HasMaxLength(1)
            .HasConversion(v => v.Valor, v => Accion.Desde(v).Valor);
        b.Property(r => r.ValorAnterior).HasColumnName("valor_anterior");
        b.Property(r => r.ValorNuevo).HasColumnName("valor_nuevo");
        b.Property(r => r.DireccionIp).HasColumnName("direccion_ip").HasMaxLength(RegistroAuditoria.LongitudDireccionIp);
        b.Property(r => r.Canal).HasColumnName("canal").HasMaxLength(5)
            .HasConversion(v => v.Valor, v => Canal.Desde(v).Valor);
        b.Property(r => r.FechaRegistro).HasColumnName("fecha_registro");
        b.Ignore(r => r.EventosDominio);
        b.Ignore(r => r.EsAccionSensible);
    }
}

internal sealed class MensajeOutboxConfiguracion : IEntityTypeConfiguration<MensajeOutbox>
{
    public void Configure(EntityTypeBuilder<MensajeOutbox> b)
    {
        b.ToTable("outbox_mensaje");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id");
        b.Property(m => m.Tipo).HasColumnName("tipo").HasMaxLength(200);
        b.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb");
        b.Property(m => m.OccurredAt).HasColumnName("occurred_at");
        b.Property(m => m.TraceParent).HasColumnName("trace_parent").HasMaxLength(55);
        b.Property(m => m.PublishedAt).HasColumnName("published_at");
        b.Property(m => m.Attempts).HasColumnName("attempts");
        b.Property(m => m.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(2000);
    }
}
