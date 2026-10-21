# SPEC-014 — Canalización de integración continua

- **Estado:** Aprobado (2026-10-21); implementado en `feat/ci-pipeline` — y el run en vivo queda pendiente del primer push (decisión del usuario).
- **Rama de implementación (propuesta):** `feat/ci-pipeline` (base: `feat/postgres-only-persistence`).
- **Requisitos relacionados:** NFR-009 («La canalización de CI ejecutará compilación y pruebas automatizadas.»).
- **Criterios relacionados:** AC-009 («La canalización ejecuta compilación y pruebas automatizadas. Un fallo bloquea la validación satisfactoria del cambio.»).
- **Decisión arquitectónica:** ninguna nueva de motor/stack; la topología de despliegue queda FUERA de esta spec (SPEC-015 posterior: GHCR + SSH + Environment `production`).
- **Contexto de producto:** el usuario gestionó la solicitud: «como nuevo requerimiento debo ir pensando en publicar mi sitio en produccion… me gustaría crear los github actions para esto». Tras SPEC-013 (PostgreSQL unified repository), el CI puede construirse sobre la misma Compose verificación local, y el repo pasará a público en breve (proyecto de master): la higiene de secretos va incluida aquí.
- **Versión:** 0.2 (revisada tras firmar la decisión: CI con Compose + gitleaks + Dependabot; despliegue aparte).

## 1. Objetivo

Un solo workflow de GitHub Actions que, en cada pull request y en cada push a `main`, compile la solución y ejecute la suite automatizada contra PostgreSQL 17 —la misma topología verificación de Compose local que la SPEC-013 demostró—, además de un barrido de secretos, de forma que un fallo bloquee la aceptación del cambio (AC-009) y el repo público no reciba secretos accidentalmente.

## 2. Alcance

### Incluido

1. **Workflow `ci.yml`** en `.github/workflows/`, con permisos mínimos (`contents: read`), triggers `pull_request` + `push` sobre `main`:
   - **Job `build-test`**: runner ubuntu; `actions/setup-dotnet` con `global-json-file` (SDK 10.0.402 fijado, sin roll-forward); `dotnet restore`, `dotnet build --configuration Debug`, y la suite completa con los dos servicios PostgreSQL levantados por la **misma `compose.yaml`** del repo (`docker compose up -d --wait app-postgres hcm-postgres`; sin `db-init`/api/web — no se construyen imágenes de la app: la suite corre en el host). Las cadenas de conexión se arman desde las credenciales por defecto del stack local (solo-locales, según `.env.example`), y `HcmPostgres__ConnectionString` con `Username=talentassistant_hcm_app` (rol de solo lectura del contrato).
   - La suite completa espera hoy **132/132 integración + 11/11 unitarias**; el único fallo admisible es el ambiental `0x800711C7` (re-ejecución y registro documentado).
   - **Job `secret-scan`**: `gitleaks` (acción oficial fijada por SHA completo) sobre todo el historial; un hallazgo bloquea.
2. **`dependabot.yml`**: actualizaciones semanales de NuGet + GitHub Actions.
3. **Checklist de settings del repo** (documentación reproducible, no scripts): secret scanning + push protection, Codespaces audit no aplica, branch protection `main` (PR + CI verde) — el usuario los activa en Settings con consentimiento.
4. **Documentación**: README + `docs/development/local-setup.md` con el flujo de la canalización y del checklist; retirada del aplazamiento de CI (NFR-009/AC-009) en el registro de decisiones.

### Fuera de alcance

- Despliegue a producción (SPEC-015 posterior: environment `production` con aprobación humana, GHCR, SSH a la VPS, rollback).
- Cobertura de código, métricas de calidad, matriz de SO, cachés agresivos: se añaden si la duración lo justifica; la prioridad es un pipeline verde y reproducible.
- Windows runners: la suite se ejecutará en ubuntu (el repo ya es LF-first); el fallo ambiental `0x800711C7` es local de la estación y no se reproduce en linux (sin Application Control).

## 3. Requisitos y límites

- Un fallo de build, de suite o de gitleaks debe marcar el pipeline rojo y bloquear la aceptación (AC-009 literal).
- La canalización NO debe usar credenciales reales ni de producción: solo las sintéticas solo-locales del contrato (`talentassistant_app`/`talentassistant_hcm_app`), y sin subidas de secretos a logs (la redacción debe verificar el pipeline en el futuro CD, no aquí).
- Reutilizar la Compose del repo como fuente de verdad del entorno de pruebas — no duplicar una segunda topología.- Sin claves de IA: ninguna prueba necesita `AI_API_KEY` (SPEC-009/011 degradación controlada; el chat sin clave responde 503 controlada — la suite lo pinta verde).
- Windows PS 5.1 no participa aquí (Linux runner): los scripts `.sh` reproducibles no requieren conversión CRLF.

## 4. Verificación

Evidencia esperada al cerrar la implementación:

- Pipeline ejecutado en PR (o push a la rama de feature): build y suite verdes, gitleaks limpio; logs del run adjuntados en el registro ODD.
- Archivos `.github/workflows/ci.yml`, `.github/dependabot.yml` creados; checklist de settings documentado en la guía.
- Aceptación de la spec y retirada del aplazamiento CI en el registro de decisiones.
