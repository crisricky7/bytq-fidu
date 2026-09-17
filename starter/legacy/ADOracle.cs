// ============================================================================
//  ADOracle.cs  -  Capa de Acceso a Datos genérica (CORE-FID)
//  .NET Framework 4.8  |  Oracle 19c  |  Autor original: 2018
//  NO MODIFICAR: utilizada por los ~40 módulos de la plataforma.
// ============================================================================
using System;
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace Core.AccessData
{
    public class ADOracle
    {
        private OracleConnection _cn;

        // Cadena de conexión del ambiente. Se cambia manualmente por ambiente.
        private const string CONNECTION_STRING =
            // Valor real retirado del repositorio: el defecto es que la credencial esté en el código.
            "User Id=COREFID;Password=<REDACTADO>;Data Source=//10.0.2.31:1521/ORCLPDB1;";

        public void Abrir()
        {
            _cn = new OracleConnection(CONNECTION_STRING);
            _cn.Open();
        }

        public void Cerrar()
        {
            if (_cn != null && _cn.State == ConnectionState.Open)
                _cn.Close();
        }

        public OracleConnection Conexion { get { return _cn; } }

        public DataTable EjecutarConsulta(string sql)
        {
            OracleCommand cmd = new OracleCommand(sql, _cn);
            OracleDataAdapter da = new OracleDataAdapter(cmd);
            DataTable dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        public int EjecutarComando(string sql)
        {
            OracleCommand cmd = new OracleCommand(sql, _cn);
            return cmd.ExecuteNonQuery();
        }

        public object EjecutarEscalar(string sql)
        {
            OracleCommand cmd = new OracleCommand(sql, _cn);
            return cmd.ExecuteScalar();
        }
    }
}
