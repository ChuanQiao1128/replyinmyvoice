---
name: cloud-architecture-cost-review
description: Use when choosing, changing, reviewing, or deploying cloud architecture, Azure/Cloudflare hosting, databases, queues, workers, CI/CD deployment targets, monthly run-rate cost, scale-to-zero behavior, or any plan that may create paid infrastructure or increase AI/provider costs.
---

# Cloud Architecture Cost Review

Use this skill as a pre-implementation architecture and cost gate. Its job is to challenge expensive or overbuilt infrastructure choices before code, CI/CD, or paid cloud resources are created.

## When To Use

Use before work involving:

- Azure App Service, Azure Functions, Container Apps, Static Web Apps, Azure SQL, Service Bus, Storage, Key Vault, Application Insights, or Entra
- Cloudflare hosting, Workers, Pages, R2, Queues, or edge/runtime architecture
- databases, queues, background workers, webhooks, always-on services, scheduled jobs, or deployment targets
- CI/CD changes that create, deploy, resize, or keep paid infrastructure running
- pricing, monthly run-rate, scale-to-zero, cold starts, log volume, provider-call costs, OpenAI/Sapling usage, or retry/evaluation budgets

Do not use this skill for backend-only state fixes, UI-only changes, or tests that do not affect architecture, cloud resources, cost, deployment, or provider usage.

## Workflow

1. State the goal, expected usage, and required runtime behavior.
2. Read relevant project docs before recommending architecture. Start with `docs/manual-setup.md`, `docs/next-development-brief.md`, `docs/dotnet-azure-full-run-result.md`, and any active target/plan document.
3. List realistic options, including the cheapest viable option.
4. Compare fixed monthly cost risk, variable usage cost, scale-to-zero behavior, operational complexity, reliability fit, and migration cost.
5. Challenge always-on or fixed-cost services for MVP, demo, low-traffic, or intermittent workloads.
6. Recommend one option and explicitly reject alternatives that are too expensive or overbuilt.
7. Identify any paid-resource action that needs user approval before execution.
8. If exact prices matter, verify current official provider pricing before quoting numbers. Do not rely on stale memory.

For a blank review, run:

```bash
python3 agent-skills/cloud-architecture-cost-review/scripts/cost_review_template.py "<topic>"
```

## Project Defaults

- For low-usage MVP or demo .NET backend work, prefer Azure Functions over Azure App Service unless Functions cannot support the runtime behavior.
- App Service is justified only when always-on ASP.NET hosting, stable low latency, long-lived connections, or App Service-specific deployment behavior is truly required.
- Treat Azure Functions, Azure SQL, and Azure Service Bus as the default low-cost Azure reliability backend shape for this project.
- Keep the public product on the existing Cloudflare/Next/Clerk/Neon path unless the task explicitly changes production architecture.
- Do not create, resize, or keep paid resources running without naming the cost risk and approval condition.
- For rewrite-quality work, include OpenAI and Sapling call counts, retry caps, staged eval mode, and estimated variable cost risk.

## Review Output

Use this structure in planning notes or final answers:

```text
Architecture Cost Review
- Goal:
- Usage assumption:
- Runtime requirements:
- Options compared:
- Fixed monthly cost risks:
- Variable usage cost risks:
- Recommended option:
- Rejected options:
- Approval gates:
- Verification needed:
- Limitations:
```

## Red Flags

- Defaulting to App Service, paid always-on compute, or premium tiers for low traffic.
- Running a background worker continuously when queue/timer triggers can scale to zero.
- Creating duplicate cloud stacks for the same demo purpose.
- Keeping high-log-volume diagnostics or verbose telemetry without retention controls.
- Using production-grade paid resources before local tests and a minimal smoke path exist.
- Running broad AI eval loops without staged modes, retry limits, call counts, or cost estimates.
- Claiming a service is cheaper without checking current provider pricing when exact numbers matter.

## Coordination

- Use this before `system-spec-synthesis` when architecture or cloud cost is still being chosen.
- Use `system-spec-synthesis` after the recommended architecture is selected.
- Use `state-machine-modeling`, `data-module-review`, and `resilience-test-generation` when the architecture affects quota, billing, webhooks, queues, retries, or persistence.
- Use `dotnet-backend-testing` or `ui-browser-testing` for implementation verification after architecture is chosen.
- Use the cloud-readiness checklist only after the architecture and cost choice is accepted.

## Evidence Rule

When this skill is used, append one entry to `docs/skill-run-log.md`. Record the selected option, rejected expensive options, pricing source if exact pricing was checked, and any limitations. Do not log secrets or raw credential values.
