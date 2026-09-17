// ============================================================================
//  EAuditoria.cs  -  Clase de Lógica de Persistencia (Entity)
//  Namespace: Business.Entities   |   .NET Framework 4.8
//  Convención CORE-FID: E[NombreEntidad] = acceso a datos, sin lógica de negocio.
// ============================================================================
using System;
using System.Data;
using System.Collections.Generic;
using Core.AccessData;
using Core.Models;

namespace Business.Entities
{
    internal class EAuditoria
    {
        /// <summary>
        /// Inserta un registro de auditoría en T01AUDITORIA.
        /// </summary>
        public bool Insertar(RegistroAuditoria reg, out string error)
        {
            error = "";
            ADOracle ado = new ADOracle();
            try
            {
                ado.Abrir();

                long secuencia = Convert.ToInt64(
                    ado.EjecutarEscalar("SELECT SQ01AUDITORIA.NEXTVAL FROM DUAL"));

                string sql =
                    "INSERT INTO T01AUDITORIA (ID, CODIGO_EMPRESA, CODIGO_MODULO, CODIGO_TRANSACCION, " +
                    "USUARIO, ENTIDAD, CLAVE_ENTIDAD, ACCION, VALOR_ANTERIOR, VALOR_NUEVO, " +
                    "DIRECCION_IP, CANAL, FECHA_REGISTRO) VALUES (" +
                    secuencia + ", '" + reg.CodigoEmpresa + "', '" + reg.CodigoModulo + "', '" +
                    reg.CodigoTransaccion + "', '" + reg.Usuario + "', '" + reg.Entidad + "', '" +
                    reg.ClaveEntidad + "', '" + reg.Accion + "', '" + reg.ValorAnterior + "', '" +
                    reg.ValorNuevo + "', '" + reg.DireccionIp + "', '" + reg.Canal + "', " +
                    "TO_DATE('" + reg.FechaRegistro.ToString("dd/MM/yyyy HH:mm:ss") + "','DD/MM/YYYY HH24:MI:SS'))";

                int filas = ado.EjecutarComando(sql);
                reg.Id = secuencia;

                ado.Cerrar();
                return filas > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Util.WriteLog("EAuditoria.Insertar", ex);
                return false;
            }
        }

        /// <summary>
        /// Búsqueda de registros de auditoría por criterios.
        /// </summary>
        public List<RegistroAuditoria> Buscar(CriterioAuditoria criterio, out string error)
        {
            error = "";
            List<RegistroAuditoria> lista = new List<RegistroAuditoria>();
            ADOracle ado = new ADOracle();
            try
            {
                ado.Abrir();

                string sql = "SELECT * FROM T01AUDITORIA WHERE CODIGO_EMPRESA = '" + criterio.CodigoEmpresa + "'";

                if (!string.IsNullOrEmpty(criterio.Usuario))
                    sql += " AND USUARIO = '" + criterio.Usuario + "'";

                if (!string.IsNullOrEmpty(criterio.Entidad))
                    sql += " AND UPPER(ENTIDAD) LIKE UPPER('%" + criterio.Entidad + "%')";

                if (criterio.FechaDesde.HasValue)
                    sql += " AND FECHA_REGISTRO >= TO_DATE('" +
                           criterio.FechaDesde.Value.ToString("dd/MM/yyyy") + "','DD/MM/YYYY')";

                sql += " ORDER BY FECHA_REGISTRO DESC";

                DataTable dt = ado.EjecutarConsulta(sql);
                foreach (DataRow fila in dt.Rows)
                    lista.Add(ArmaObjeto(fila));

                ado.Cerrar();
                return lista;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Util.WriteLog("EAuditoria.Buscar", ex);
                return lista;
            }
        }

        private RegistroAuditoria ArmaObjeto(DataRow fila)
        {
            RegistroAuditoria r = new RegistroAuditoria();
            r.Id = Convert.ToInt64(fila["ID"]);
            r.CodigoEmpresa = fila["CODIGO_EMPRESA"].ToString();
            r.CodigoModulo = fila["CODIGO_MODULO"].ToString();
            r.CodigoTransaccion = fila["CODIGO_TRANSACCION"].ToString();
            r.Usuario = fila["USUARIO"].ToString();
            r.Entidad = fila["ENTIDAD"].ToString();
            r.ClaveEntidad = fila["CLAVE_ENTIDAD"].ToString();
            r.Accion = fila["ACCION"].ToString();
            r.ValorAnterior = fila["VALOR_ANTERIOR"].ToString();
            r.ValorNuevo = fila["VALOR_NUEVO"].ToString();
            r.DireccionIp = fila["DIRECCION_IP"].ToString();
            r.Canal = fila["CANAL"].ToString();
            r.FechaRegistro = Convert.ToDateTime(fila["FECHA_REGISTRO"]);
            return r;
        }
    }
}
