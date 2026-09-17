# Token de desarrollo para el ejercicio

No hay tenant de Entra ID disponible para la prueba. Simula la validación con una
clave simétrica (HS256). En producción esto será RS256 contra el endpoint JWKS de
Entra ID — la diferencia debe quedar aislada en configuración, no en el código.

```
Issuer:   https://sts.windows.net/eval-tenant/
Audience: api://corefid-audit
Secret:   (entregado en el material del ejercicio; en este repositorio se configura en JWT_CLAVE_FIRMA vía .env)
Algoritmo: HS256
```

Claims que el servicio debe poder leer:

```json
{
  "iss": "https://sts.windows.net/eval-tenant/",
  "aud": "api://corefid-audit",
  "sub": "b3f1a2c4-1111-2222-3333-444455556666",
  "preferred_username": "jperez",
  "roles": ["audit.write", "audit.read"],
  "exp": 1893456000
}
```

Puedes generar tokens de prueba en https://jwt.io con esos valores, o escribir un
pequeño helper en los tests.
