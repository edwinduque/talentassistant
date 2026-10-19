# OWASP Top 10:2025 and ASVS mapping — Talent Assistant surfaces

Reference aid for classification. It does NOT guarantee coverage; combined with `checklist.md` items. ASVS ids point to the chapter/vector families (verify against the installed ASVS version when quoting precisely).

| TA surface | Primary OWASP 2025 | ASVS family | Checklist items |
| --- | --- | --- | --- |
| Endpoints without auth, IDOR on employee data, UI-only controls | A01 Broken Access Control | V4 Authorization, V6 Access Control | 2.1–2.5 |
| JWT signing key, cookie flags, transport to HCM DB | A02 Cryptographic Failures | V3 Cryptography, V9 Communication | 1.1–1.2, 7.2 |
| SQL (EF/Npgsql), prompt injection, hostile tool arguments, form inputs | A03 Injection | V5 Validation, Sanitization and Encoding | 4.1–4.6 |
| Trust boundaries, tool catalog design, single-tenant assumptions, missing security requirements in specs | A04 Insecure Design | V1 Architecture, V10 Requirements | spec-mode items, 2.3–2.4, 8.1 |
| Compose defaults, env secrets, fail-fast config, dev/prod parity | A05 Security Misconfiguration | V14 Configuration | 3.3, 5.1–5.4, 7.1, 10.1–10.2 |
| Login, lockout, password hashing, session/token lifetime | A07 Identification and Authentication Failures | V2 Authentication, V3 Session | 1.3–1.4 |
| Dependency pinning, test-host packages, image provenance | A08 Software and Data Integrity Failures | V11 Business Logic / SDLC | 6.1–6.3 |
| Absence of audit trail for logins/chats | A09 Security Logging and Monitoring Failures | V7 Error Handling and Logging | 3.2, 10.4 |
| SSRF | A10 Server-Side Request Forgery | V12 Files and Resources | 7.3 (`Not applicable`, justified) |

Reporting rules: cite the category as `A0X (name)` and the ASVS family (e.g., `ASVS V4`); if a finding spans categories, pick the primary one and mention the secondary. For AI-specific risks without a clean OWASP slot, classify under A04 (Insecure Design) or A03 (Injection — prompt injection) and say so explicitly.

Honesty caveat to include in every report: this mapping is a checklist aid, not evidence of coverage; areas reviewed by AI without executed verification remain `Not verified`, and this process is not a substitute for a professional security audit.
