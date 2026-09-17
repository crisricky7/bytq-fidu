// Genera un JWT HS256 de desarrollo con los valores de starter/JWT-DEV.md.
// Uso: node scripts/token-dev.mjs [roles separados por coma]
import { createHmac } from "node:crypto";

const clave = process.env.JWT_CLAVE_FIRMA ?? "<REDACTADO>";
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
