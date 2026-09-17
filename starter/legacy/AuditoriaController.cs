// ============================================================================
//  AuditoriaController.cs  -  Controlador REST (CORE-FID API)
//  Namespace: CoreFidAPI.Controllers   |   ASP.NET Web API 2 / .NET Framework 4.8
// ============================================================================
using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Http;
using Business.Core.Auditoria;
using Core.Models;

namespace CoreFidAPI.Controllers
{
    [Authorize]
    [RoutePrefix("api/auditoria")]
    public class AuditoriaController : ApiController
    {
        private BAuditoria bAuditoria = new BAuditoria();

        /// <summary>Registra un evento de auditoría.</summary>
        [HttpPost, Route("registro")]
        public IHttpActionResult Registrar([FromBody] RegistroAuditoriaRequest request)
        {
            Util.WriteLogApi("AuditoriaController.Registrar", "IN", request);
            try
            {
                if (request == null)
                    return Content(HttpStatusCode.BadRequest, Util.CreateResponse("E00", "Petición vacía."));

                // El canal BATCH no puede registrar consultas (regla operativa).
                if (request.Canal == "BATCH" && request.Accion == "Q")
                    return Content(HttpStatusCode.BadRequest,
                        Util.CreateResponse("E10", "El canal BATCH no registra consultas."));

                RegistroAuditoria reg = new RegistroAuditoria
                {
                    CodigoEmpresa = request.CodigoEmpresa,
                    CodigoModulo = request.CodigoModulo,
                    CodigoTransaccion = request.CodigoTransaccion,
                    Usuario = request.Usuario,
                    Entidad = request.Entidad,
                    ClaveEntidad = request.ClaveEntidad,
                    Accion = request.Accion,
                    ValorAnterior = request.ValorAnterior,
                    ValorNuevo = request.ValorNuevo,
                    DireccionIp = request.DireccionIp,
                    Canal = request.Canal
                };

                ResponseCreate<long> resp = bAuditoria.Registrar(reg);
                Util.WriteLogApi("AuditoriaController.Registrar", "OUT", resp);
                return Ok(resp);
            }
            catch (Exception ex)
            {
                Util.WriteLog("AuditoriaController.Registrar", ex);
                return Ok(Util.CreateResponse("OK", "Procesado."));
            }
        }

        /// <summary>Consulta registros de auditoría.</summary>
        [HttpGet, Route("consulta")]
        public IHttpActionResult Consultar(string codigoEmpresa, string usuario = null,
                                           string entidad = null, DateTime? fechaDesde = null)
        {
            try
            {
                CriterioAuditoria criterio = new CriterioAuditoria
                {
                    CodigoEmpresa = codigoEmpresa,
                    Usuario = usuario,
                    Entidad = entidad,
                    FechaDesde = fechaDesde
                };

                List<RegistroAuditoria> lista = bAuditoria.Consultar(criterio);
                return Ok(Util.CreateResponse("OK", "Consulta exitosa", lista));
            }
            catch (Exception ex)
            {
                Util.WriteLog("AuditoriaController.Consultar", ex);
                return Content(HttpStatusCode.InternalServerError,
                    Util.CreateResponse("E99", ex.Message, ex.StackTrace));
            }
        }
    }
}
