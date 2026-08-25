# Padel Court Management System - Living Plan

**Status:** Pre-implementation
**Last updated:** 2026-08-21
**Purpose:** This is the project source of truth for scope, decisions, delivery state, and context-reset recovery. Update it in every pull request that changes a decision, milestone, risk, or delivery state.

## Quick Restart

When starting a new Copilot context or inviting an agent:

1. Read this file first.
2. Read `docs/decisions.md` and the open GitHub issues/PRs.
3. Check the current branch, pull request status, CI, and `git status`.
4. Continue only the current milestone; do not rework completed decisions without recording a new decision.

## Objective

Build a padel-court management system from `../firstinstructions.txt`.

- **Frontend:** React + TypeScript
- **Backend:** ASP.NET Core REST API
- **Database:** SQL Server
- **Architecture:** Domain / Application / Infrastructure / API, with dependency injection
- **Delivery:** GitHub Issues, short-lived branches, pull requests, required CI, and merge history

The `../SGBD2025` repository is a structural and delivery reference only. The FIFA dossier is a documentation reference: requirements analysis, use cases, rule traceability, data model, and technical architecture.

## Delivery Rules

- One issue per independently reviewable outcome.
- One short-lived branch per issue: `<issue-number>-<topic>`.
- One focused PR per branch, linked to its issue.
- PR descriptions must name affected business-rule IDs, API/schema/UI effects, tests, and decision-log changes.
- Merge only after required CI passes; keep `main` deployable.
- Record a decision before implementing a meaningful architectural, schema, security, or ambiguous-business-rule choice.
- Do not commit credentials. Use separate least-privilege database accounts for schema deployment and application runtime.

## Milestones

| ID | Milestone | Status | Definition of done |
| --- | --- | --- | --- |
| M0 | Repository foundation | In progress | Local Git repository, backend solution scaffold, frontend placeholder, CI baseline, README, templates, and local setup work. |
| M1 | Analysis and decision record | Not started | Glossary, actors, use cases, business-rule register, ER model, relational schema, and initial decisions are reviewed. |
| M2 | Database and security | Not started | Versioned schema, seed data, constraints, indexes, roles/users, and permission documentation exist. |
| M3 | Identity and administration | Not started | Matricule identification, administrator authorization, and site/court/schedule/closure management work. |
| M4 | Reservations | Not started | Availability, booking windows, site restrictions, private/public matches, conversion, and bans work. |
| M5 | Payments and debts | Not started | Payments, paid-seat ordering, balances, and debt enforcement work. |
| M6 | React client | Not started | Member and administrator workflows consume the REST API. |
| M7 | Reporting and delivery hardening | Not started | Statistics, tests, CI reporting, demo data, runbook, and traceability are complete. |

## Initial Architecture Decisions

| ID | Decision | Choice | Reason |
| --- | --- | --- | --- |
| D-001 | Application split | React/TypeScript client and ASP.NET Core REST API in separate folders | Meets the specification and permits independent deployment/testing. |
| D-002 | Backend layers | Domain, Application, Infrastructure, API with interfaces and DI | Keeps policy, transport, and SQL concerns separate. |
| D-003 | Database | SQL Server with versioned SQL scripts | Matches the relational and security objectives. |
| D-004 | Rule enforcement | Application services enforce workflows; database constraints and transactions protect invariants | Provides clear errors while keeping data safe from every client. |
| D-005 | Payment integration | Internal payment simulation unless a real provider is explicitly approved | Avoids introducing an unscoped payment/security integration. |
| D-006 | Time-based rules | A scheduled backend job processes next-day conversion, bans, and outstanding balances | Rules must run even when no one opens the application. |

## Business Rules to Trace

| ID | Rule | Planned enforcement |
| --- | --- | --- |
| BR-001 | Court reservations last 90 minutes and require a 15-minute gap. | Availability service, database transaction, and integration tests. |
| BR-002 | Global/site/free members have 3-week/2-week/5-day booking windows and site restrictions. | Authorization and reservation service. |
| BR-003 | Private matches need four players; incomplete matches become public the day before and the creator receives a one-week booking ban. | Scheduled job and reservation service. |
| BR-004 | Public-match seats go to the first members whose payments succeed. | Payment transaction and unique/locking strategy. |
| BR-005 | A private-match unpaid player loses their seat the day before; the match becomes public. | Scheduled job and payment/reservation service. |
| BR-006 | A public-match organizer pays the unfilled balance and cannot make a reservation while in debt. | Payment/debt service and reservation service. |
| BR-007 | An indebted organizer registering for another match has debt added to that payment. | Payment service transaction. |

## Open Decisions

| ID | Question | Needed before | Owner | Status |
| --- | --- | --- | --- | --- |
| O-001 | How are members and administrators provisioned when login uses only a matricule? | M3 | Team | Open |
| O-002 | Which time zone governs booking windows and scheduled next-day processing? | M4 | Team | Open |
| O-003 | What happens to existing reservations when a site/court is closed? | M3 | Team | Open |
| O-004 | Are cancellation, refund, and no-show policies in scope? | M4/M5 | Team | Open |
| O-005 | Does “pay” mean simulated payment or an approved external provider? | M5 | Team | Defaulted to simulation |

## Current Work

**Current milestone:** M0 - Repository foundation
**Current issue/branch/PR:** Local M0 foundation commit on `main`; GitHub repository and issue/PR not yet created
**Next action:** Create the remote GitHub repository, push `main`, then install Node.js and scaffold the React/TypeScript client.

## Context Handoff Template

Copy this into a new context or agent prompt:

```text
Read PROJECT_PLAN.md, docs/decisions.md, the current GitHub issue/PR, and git status before acting.
Current milestone: <M#>.
Current issue/branch/PR: <links or identifiers>.
Implement only the scoped issue. Preserve recorded decisions, update PROJECT_PLAN.md and docs/decisions.md if the work changes them, run the relevant checks, and report changed files plus any unresolved decision.
```

## Multi-Agent Working Agreement

- Give each agent one issue with a non-overlapping file or feature boundary.
- Keep schema/architecture changes in a dedicated issue and merge them before dependent API or frontend work.
- Use research/review agents read-only; let only the implementation agent edit its assigned branch.
- Every agent handoff must include the issue, branch, accepted decisions, affected business-rule IDs, and required checks.
- Use a final review agent on a completed PR to verify rule traceability, transaction/concurrency safety, privilege scope, and tests before merging.
