// ============================================================================
//  Models.cs  -  DTOs del módulo de Auditoría (namespace Core.Models)
// ============================================================================
using System;

namespace Core.Models
{
    public class RegistroAuditoria
    {
        /// <summary>Identificador secuencial del registro.</summary>
        public long Id { get; set; }
        /// <summary>Código de la empresa (multiempresa).</summary>
        public string CodigoEmpresa { get; set; }
        /// <summary>Código numérico (00-99) del módulo o subsistema.</summary>
        public string CodigoModulo { get; set; }
        /// <summary>Código de transacción (pantalla VAABCCC) que originó el evento.</summary>
        public string CodigoTransaccion { get; set; }
        /// <summary>Usuario de red o de API que ejecutó la acción.</summary>
        public string Usuario { get; set; }
        /// <summary>Nombre de la entidad de negocio afectada.</summary>
        public string Entidad { get; set; }
        /// <summary>Clave primaria del registro afectado.</summary>
        public string ClaveEntidad { get; set; }
        /// <summary>C = Crear, U = Actualizar, D = Eliminar, Q = Consultar.</summary>
        public string Accion { get; set; }
        /// <summary>Snapshot JSON previo al cambio.</summary>
        public string ValorAnterior { get; set; }
        /// <summary>Snapshot JSON posterior al cambio.</summary>
        public string ValorNuevo { get; set; }
        /// <summary>IP de origen de la petición.</summary>
        public string DireccionIp { get; set; }
        /// <summary>WEB, API o BATCH.</summary>
        public string Canal { get; set; }
        /// <summary>Fecha y hora del registro.</summary>
        public DateTime FechaRegistro { get; set; }
    }

    public class RegistroAuditoriaRequest
    {
        public string CodigoEmpresa { get; set; }
        public string CodigoModulo { get; set; }
        public string CodigoTransaccion { get; set; }
        public string Usuario { get; set; }
        public string Entidad { get; set; }
        public string ClaveEntidad { get; set; }
        public string Accion { get; set; }
        public string ValorAnterior { get; set; }
        public string ValorNuevo { get; set; }
        public string DireccionIp { get; set; }
        public string Canal { get; set; }
    }

    public class CriterioAuditoria
    {
        public string CodigoEmpresa { get; set; }
        public string Usuario { get; set; }
        public string Entidad { get; set; }
        public DateTime? FechaDesde { get; set; }
    }

    public class ResponseCreate<T>
    {
        public string Codigo { get; set; }
        public string Mensaje { get; set; }
        public string Detalle { get; set; }
        public T Data { get; set; }
    }
}
