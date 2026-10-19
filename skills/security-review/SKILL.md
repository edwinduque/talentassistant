---
name: security-review
description: "Trigger: security review, revisión de seguridad, threat model, OWASP, ASVS. Review Talent Assistant specs, diffs, or code for evidenced security risks."
license: Apache-2.0
metadata:
  author: "edwinduque"
  version: "1.0"
---

# Security review for Talent Assistant

## Activation Contract

Load when the user asks for a security review, "revisión de seguridad", threat model, or an OWASP/ASVS check; before approving a spec under `specs/`; after a vertical (task/commit) is implemented; or at milestone audit requests. Target repository: the current workspace (Talent Assistant).

## Hard Rules

- Read-only audit: never modify code, configuration, Windows policy, certificates, or secrets; install no packages, plugins, or tools; run no destructive tests and no attacks against external systems.
- Report secret locations and types only — never values, tokens, or connection strings.
- Classify every applicable checklist item with exactly one state; never claim a control is implemented from documentation alone, and never mark the audit approved while relevant areas are unverified.
- Separate AI-assisted review from automated checks actually executed: name every command you ran and its observed result; anything not executed is `Not verified`.
- Always state limitations: OWASP Top 10:2025, ASVS, and Cheat Sheet references do not guarantee complete coverage, and an AI-assisted review is not a professional security audit.

## Decision Gates

| Mode | Use when | Depth |
| --- | --- | --- |
| `spec` | a `specs/**` change awaits approval | assets, actors, trust boundaries, missing security requirements, proposed security ACs |
| `diff` | a vertical/commit exists | changed code plus its trust-boundary impact; prior controls re-checked only where touched |
| `audit` | milestone request | full checklist in `references/checklist.md` |

Severity per finding: `Critical|High|Medium|Low` plus one-line justification. Confidence: `High|Medium|Low` plus what was inspected. Single-tenant PoC scope: multi-company isolation items are `Not applicable` with justification until a real multi-org HCM arrives.

## Execution Steps

1. Read the target: spec + ODD plan + `## Log` for `spec` mode; `git diff <base>..HEAD` plus neighbors for `diff` mode; repo sweep for `audit`. List the trust boundaries touched (browser↔Web, Web↔API, API↔MySQL, API↔HCM Postgres, API↔Groq, model↔tools).
2. Load `references/checklist.md` and `references/owasp-mapping.md`; enumerate assets, actors, and data flows in scope.
3. Verify each applicable item against evidence: read code, grep symbols, run read-only commands; cite `file:symbol` or `file:line` for every claim.
4. Classify findings, assign severity/confidence, cross-reference OWASP Top 10:2025 category and ASVS id where applicable.
5. Produce the report from `assets/report-template.md`; translate findings into proposed SDD acceptance criteria or task candidates without editing any file.

Examples: `revisión de seguridad de specs/011-hcm-postgres-source` (spec) · `security review del diff e920a33..6d78234` (diff) · `auditoría profunda de autenticación y sesión` (audit).

## Output Contract

One report using `assets/report-template.md`: summary table, every finding with the nine required fields and a classification state, the list of verification commands actually executed, the unverified areas, and the limitations statement. End by proposing which findings deserve SDD tasks or acceptance criteria; make no file changes.

## References

- `references/checklist.md` — Talent Assistant security checklist (adapted to .NET 10 / ASP.NET Core / Blazor / EF Core / MySQL / HCM connector / assistant).
- `references/owasp-mapping.md` — TA surfaces mapped to OWASP Top 10:2025 and ASVS chapters.
- `assets/report-template.md` — required report format.
