// Genera un JWT HS256 de desarrollo con issuer/audience de starter/JWT-DEV.md.
// La clave se toma de JWT_CLAVE_FIRMA (variable de entorno o archivo .env en la raíz).
// Uso: node scripts/token-dev.mjs [roles separados por coma]
import { createHmac } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";

const rutaEnv = new URL("../.env", import.meta.url);
const desdeEnv = existsSync(rutaEnv)
  ? readFileSync(rutaEnv, "utf8").split(/\r?\n/).find((l) => l.startsWith("JWT_CLAVE_FIRMA="))?.slice("JWT_CLAVE_FIRMA=".length)
  : undefined;

const clave = process.env.JWT_CLAVE_FIRMA ?? desdeEnv;
if (!clave || clave.length < 32) {
  console.error("Defina JWT_CLAVE_FIRMA (mínimo 32 caracteres) o ejecute scripts/init-env.sh.");
  process.exit(1);
}

const roles = (process.argv[2] ?? "audit.write,audit.read").split(",");
const ahora = Math.floor(Date.now() / 1000);
const b64 = (o) => Buffer.from(JSON.stringify(o)).toString("base64url");

const encabezado = b64({ alg: "HS256", typ: "JWT" });
const cuerpo = b64({
  iss: "https://sts.windows.net/eval-tenant/",
  aud: "api://corefid-audit",
  sub: "b3f1a2c4-1111-2222-3333-444455556666",
  preferred_username: "jperez",
  roles,
  iat: ahora,
  nbf: ahora,
  exp: ahora + 3600,
});
const firma = createHmac("sha256", clave).update(`${encabezado}.${cuerpo}`).digest("base64url");
console.log(`${encabezado}.${cuerpo}.${firma}`);
