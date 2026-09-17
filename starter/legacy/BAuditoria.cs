// ============================================================================
//  BAuditoria.cs  -  Clase de Lógica de Negocio (Business)
//  Namespace: Business.Core.Auditoria   |   .NET Framework 4.8
//  Convención CORE-FID: B[NombreEntidad] = reglas de negocio, delega persistencia.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Transactions;
using Business.Entities;
using Core.Models;

namespace Business.Core.Auditoria
{
    public class BAuditoria
    {
        private EAuditoria eAuditoria = new EAuditoria();
        private ENotificacion eNotificacion = new ENotificacion();

        /// <summary>
        /// Registra un evento de auditoría y notifica a Seguridad si es una acción sensible.
        /// Usado por SIAFI Web, SIAFI API y SIAFI Batch.
        /// </summary>
        public ResponseCreate<long> Registrar(RegistroAuditoria reg)
        {
            ResponseCreate<long> resp = new ResponseCreate<long>();
            string error = "";

            // -------- Validaciones de negocio --------
            if (string.IsNullOrEmpty(reg.CodigoEmpresa))
            {
                resp.Codigo = "E01"; resp.Mensaje = "Código de empresa requerido."; return resp;
            }
            if (string.IsNullOrEmpty(reg.Usuario))
            {
                resp.Codigo = "E02"; resp.Mensaje = "Usuario requerido."; return resp;
            }
            if (reg.Accion != "C" && reg.Accion != "U" && reg.Accion != "D" && reg.Accion != "Q")
            {
                resp.Codigo = "E03"; resp.Mensaje = "Acción inválida."; return resp;
            }
            if (reg.Accion == "U" && string.IsNullOrEmpty(reg.ValorAnterior))
            {
                resp.Codigo = "E04"; resp.Mensaje = "Una actualización requiere valor anterior."; return resp;
            }
            if (string.IsNullOrEmpty(reg.ClaveEntidad))
            {
                resp.Codigo = "E05"; resp.Mensaje = "Clave de entidad requerida."; return resp;
            }

            reg.FechaRegistro = DateTime.Now;
            if (string.IsNullOrEmpty(reg.Canal)) reg.Canal = "WEB";

            // -------- Persistencia + notificación --------
            using (TransactionScope ts = new TransactionScope())
            {
                bool ok = eAuditoria.Insertar(reg, out error);
                if (!ok)
                {
                    resp.Codigo = "E99";
                    resp.Mensaje = "No se pudo registrar la auditoría.";
                    resp.Detalle = error;
                    return resp;
                }

                // Las acciones sensibles se notifican al buzón de Seguridad.
                if (EsAccionSensible(reg))
                {
                    string errorMail = "";
                    eNotificacion.EnviarCorreo(
                        "seguridad@corefid.com.ec",
                        "Acción sensible: " + reg.Entidad + " / " + reg.Accion,
                        ConstruirCuerpo(reg),
                        out errorMail);
                    // si falla el correo no se interrumpe el registro
                }

                ts.Complete();
            }

            resp.Codigo = "OK";
            resp.Mensaje = "Registro creado.";
            resp.Data = reg.Id;
            return resp;
        }

        public List<RegistroAuditoria> Consultar(CriterioAuditoria criterio)
        {
            string error = "";
            return eAuditoria.Buscar(criterio, out error);
        }

        private bool EsAccionSensible(RegistroAuditoria reg)
        {
            return reg.Accion == "D"
                || reg.CodigoModulo == "06"   // Fondos de Inversión
                || reg.Entidad == "Usuario"
                || reg.Entidad == "Rol";
        }

        private string ConstruirCuerpo(RegistroAuditoria reg)
        {
            return "Usuario: " + reg.Usuario + "<br/>Entidad: " + reg.Entidad +
                   "<br/>Clave: " + reg.ClaveEntidad + "<br/>Acción: " + reg.Accion +
                   "<br/>Fecha: " + reg.FechaRegistro.ToString("dd/MM/yyyy HH:mm:ss") +
                   "<br/>Valor anterior: " + reg.ValorAnterior +
                   "<br/>Valor nuevo: " + reg.ValorNuevo;
        }
    }
}
