#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import datetime as dt
import math
import os
import sqlite3
import struct
import sys
import urllib.parse
import zlib
from collections import Counter, defaultdict
from dataclasses import dataclass
from decimal import Decimal
from pathlib import Path
from typing import Any, Tuple


STATUS_ORDER = ["success", "quality_failed", "server_failed"]
CHART_FILES = {
    "daily_volume": "daily_rewrite_volume.png",
    "status_breakdown": "success_failure_breakdown.png",
    "signal_distribution": "signal_improvement_distribution.png",
    "signal_scatter": "draft_vs_rewrite_signal_scatter.png",
    "cost_over_time": "cost_per_successful_rewrite_over_time.png",
    "strategy_comparison": "strategy_version_comparison.png",
}


class AnalysisError(Exception):
    pass


@dataclass
class QueryResult:
    rows: list[dict[str, Any]]
    provider_rows: list[dict[str, Any]]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build an offline Rewrite Quality Analysis report from telemetry tables.",
    )
    parser.add_argument("--since", help="Inclusive report start date, YYYY-MM-DD.")
    parser.add_argument("--until", help="Exclusive report end date, YYYY-MM-DD.")
    parser.add_argument(
        "--output",
        default="docs/rewrite-quality-analysis-report.md",
        help="Markdown report path.",
    )
    parser.add_argument(
        "--summary-csv",
        default="exports/rewrite-quality-summary.csv",
        help="CSV summary path.",
    )
    parser.add_argument(
        "--charts-dir",
        default="exports/charts",
        help="Directory for PNG charts.",
    )
    parser.add_argument(
        "--database-url-env",
        default="DATABASE_URL",
        help="Environment variable that contains the read-only database URL.",
    )
    return parser.parse_args()


def validate_date(value: str | None, name: str) -> str | None:
    if value is None:
        return None
    try:
        dt.date.fromisoformat(value)
    except ValueError as exc:
        raise AnalysisError(f"{name} must use YYYY-MM-DD format.") from exc
    return value


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def read_database_url(env_name: str) -> str:
    value = os.environ.get(env_name)
    if not value:
        raise AnalysisError(
            f"Missing database URL. Set {env_name} to a read-only telemetry database connection.",
        )
    return value


def sqlite_path_from_url(parsed: urllib.parse.ParseResult) -> str:
    if parsed.netloc and parsed.netloc not in {"", "localhost"}:
        raise AnalysisError("SQLite database URL must point to a local file path.")
    path = urllib.parse.unquote(parsed.path)
    if not path:
        raise AnalysisError("SQLite database URL is missing a file path.")
    return path


class Database:
    def __init__(self, url: str):
        self.url = url
        self.parsed = urllib.parse.urlparse(url)
        self.dialect = self.parsed.scheme
        self.connection: Any = None

    @property
    def placeholder(self) -> str:
        return "?" if self.dialect == "sqlite" else "%s"

    def __enter__(self) -> "Database":
        if self.dialect == "sqlite":
            sqlite_path = sqlite_path_from_url(self.parsed)
            sqlite_uri = Path(sqlite_path).resolve().as_uri() + "?mode=ro"
            self.connection = sqlite3.connect(sqlite_uri, uri=True)
            self.connection.row_factory = sqlite3.Row
            self.connection.execute("PRAGMA query_only = ON")
            return self

        if self.dialect in {"postgres", "postgresql"}:
            self.dialect = "postgres"
            self.connection = self._connect_postgres()
            self._set_postgres_read_only()
            return self

        raise AnalysisError(
            "Unsupported database URL scheme. Use sqlite, postgres, or postgresql.",
        )

    def __exit__(self, *_args: object) -> None:
        if self.connection is not None:
            self.connection.close()

    def _connect_postgres(self) -> Any:
        try:
            import psycopg  # type: ignore[import-not-found]

            return psycopg.connect(self.url)
        except ModuleNotFoundError:
            pass

        try:
            import psycopg2  # type: ignore[import-not-found]

            return psycopg2.connect(self.url)
        except ModuleNotFoundError as exc:
            raise AnalysisError(
                "Postgres analysis requires the psycopg or psycopg2 Python package.",
            ) from exc

    def _set_postgres_read_only(self) -> None:
        cursor = self.connection.cursor()
        try:
            cursor.execute("SET default_transaction_read_only = on")
        finally:
            cursor.close()

    def fetch_dicts(self, sql: str, params: list[Any]) -> list[dict[str, Any]]:
        cursor = self.connection.cursor()
        try:
            cursor.execute(sql, params)
            columns = [description[0] for description in cursor.description]
            rows = cursor.fetchall()
        finally:
            cursor.close()

        result: list[dict[str, Any]] = []
        for row in rows:
            if isinstance(row, sqlite3.Row):
                result.append(dict(row))
            else:
                result.append(dict(zip(columns, row)))
        return result


def build_where(
    placeholder: str,
    since: str | None,
    until: str | None,
    alias: str = "l",
) -> tuple[str, list[Any]]:
    clauses: list[str] = []
    params: list[Any] = []
    if since:
        clauses.append(f'{alias}."startedAt" >= {placeholder}')
        params.append(since)
    if until:
        clauses.append(f'{alias}."startedAt" < {placeholder}')
        params.append(until)
    if not clauses:
        return "", params
    return "WHERE " + " AND ".join(clauses), params


def fetch_telemetry(database_url: str, since: str | None, until: str | None) -> QueryResult:
    with Database(database_url) as db:
        where_sql, params = build_where(db.placeholder, since, until)
        provider_where_sql, provider_params = build_where(db.placeholder, since, until)
        request_sql = f"""
            SELECT
              l."id" AS "costLogId",
              l."requestId",
              l."strategyVersion",
              l."scenario",
              l."tonePreset",
              l."status",
              l."errorCode",
              l."startedAt",
              l."finishedAt",
              l."durationMs",
              l."draftAiLikePercent",
              l."rewriteAiLikePercent",
              l."changePoints",
              l."internalStrategies",
              l."repairCandidates",
              l."rejectedCandidates",
              l."usedEscalation",
              l."openAiInputTokens",
              l."openAiOutputTokens",
              l."openAiCostUsd",
              l."saplingCallCount",
              l."saplingCharacters",
              l."saplingCostUsd",
              l."totalEstimatedCostUsd",
              l."createdAt"
            FROM "RewriteCostLog" l
            {where_sql}
            ORDER BY l."startedAt" ASC, l."createdAt" ASC
        """
        provider_sql = f"""
            SELECT
              p."costLogId",
              p."provider",
              p."role",
              p."model",
              p."inputTokens",
              p."outputTokens",
              p."characters",
              p."estimatedCostUsd",
              p."latencyMs",
              p."success",
              p."errorCode",
              p."createdAt"
            FROM "RewriteProviderCall" p
            INNER JOIN "RewriteCostLog" l ON l."id" = p."costLogId"
            {provider_where_sql}
            ORDER BY p."createdAt" ASC
        """
        try:
            return QueryResult(
                rows=db.fetch_dicts(request_sql, params),
                provider_rows=db.fetch_dicts(provider_sql, provider_params),
            )
        except Exception as exc:
            raise AnalysisError(
                "Telemetry schema is not ready for analysis. Check M5-002/M5-004 "
                "columns on RewriteCostLog and RewriteProviderCall.",
            ) from exc


def number(value: Any) -> float:
    if value is None:
        return 0.0
    if isinstance(value, Decimal):
        return float(value)
    try:
        parsed = float(value)
    except (TypeError, ValueError):
        return 0.0
    if not math.isfinite(parsed):
        return 0.0
    return parsed


def optional_number(value: Any) -> float | None:
    if value is None:
        return None
    parsed = number(value)
    return parsed if math.isfinite(parsed) else None


def bool_value(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    if isinstance(value, str):
        return value.lower() in {"1", "true", "t", "yes"}
    return False


def parse_datetime(value: Any) -> dt.datetime | None:
    if isinstance(value, dt.datetime):
        return value
    if isinstance(value, str):
        text = value.strip()
        if not text:
            return None
        if text.endswith("Z"):
            text = text[:-1] + "+00:00"
        try:
            return dt.datetime.fromisoformat(text)
        except ValueError:
            return None
    return None


def day_bucket(row: dict[str, Any]) -> str:
    parsed = parse_datetime(row.get("startedAt")) or parse_datetime(row.get("createdAt"))
    if parsed is None:
        return "unknown"
    return parsed.date().isoformat()


def percent(numerator: float, denominator: float) -> float | None:
    if denominator == 0:
        return None
    return (numerator / denominator) * 100


def mean(values: list[float]) -> float | None:
    if not values:
        return None
    return sum(values) / len(values)


def percentile(values: list[float], p: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    rank = (len(ordered) - 1) * p
    low = math.floor(rank)
    high = math.ceil(rank)
    if low == high:
        return ordered[low]
    return ordered[low] + (ordered[high] - ordered[low]) * (rank - low)


def safe_label(value: Any, fallback: str = "unknown") -> str:
    text = str(value or fallback).strip() or fallback
    allowed = []
    for char in text[:80]:
        if char.isalnum() or char in {" ", "_", "-", ".", "/", ":"}:
            allowed.append(char)
    return "".join(allowed).strip() or fallback


@dataclass
class Analysis:
    period: str
    total_requests: int
    status_counts: Counter[str]
    failure_counts: Counter[str]
    provider_error_counts: Counter[str]
    signal_unavailable_count: int
    measured_count: int
    average_signal_drop: float | None
    no_regression_rate: float | None
    below_threshold_rate: float | None
    fact_failure_count: int
    total_cost: float
    openai_cost: float
    sapling_cost: float
    sapling_calls: int
    cost_per_success: float | None
    duration_p50: float | None
    duration_p95: float | None
    escalation_rate: float | None
    average_internal_strategies: float | None
    daily_counts: dict[str, Counter[str]]
    daily_cost_per_success: dict[str, float]
    signal_drops: list[float]
    signal_pairs: list[tuple[float, float]]
    strategy_rows: list[dict[str, Any]]


def analyze(
    rows: list[dict[str, Any]],
    provider_rows: list[dict[str, Any]],
    period: str,
) -> Analysis:
    total_requests = len(rows)
    status_counts = Counter(safe_label(row.get("status")) for row in rows)
    success_rows = [row for row in rows if row.get("status") == "success"]

    failure_counts: Counter[str] = Counter()
    signal_unavailable_count = 0
    signal_drops: list[float] = []
    signal_pairs: list[tuple[float, float]] = []
    no_regression_count = 0
    below_threshold_count = 0

    for row in rows:
        status = safe_label(row.get("status"))
        error_code = safe_label(row.get("errorCode"), status)
        if status != "success":
            failure_counts[error_code] += 1

        draft_signal = optional_number(row.get("draftAiLikePercent"))
        rewrite_signal = optional_number(row.get("rewriteAiLikePercent"))
        if draft_signal is None or rewrite_signal is None:
            signal_unavailable_count += 1
            continue
        drop = draft_signal - rewrite_signal
        signal_drops.append(drop)
        signal_pairs.append((draft_signal, rewrite_signal))
        if drop >= 0:
            no_regression_count += 1
        if rewrite_signal < 50:
            below_threshold_count += 1

    provider_error_counts: Counter[str] = Counter()
    for provider_row in provider_rows:
        if bool_value(provider_row.get("success")):
            continue
        provider = safe_label(provider_row.get("provider"))
        code = safe_label(provider_row.get("errorCode"), "provider_failed")
        provider_error_counts[f"{provider}:{code}"] += 1

    durations = [number(row.get("durationMs")) for row in rows if row.get("durationMs") is not None]
    total_cost = sum(number(row.get("totalEstimatedCostUsd")) for row in rows)
    success_cost = sum(number(row.get("totalEstimatedCostUsd")) for row in success_rows)
    openai_cost = sum(number(row.get("openAiCostUsd")) for row in rows)
    sapling_cost = sum(number(row.get("saplingCostUsd")) for row in rows)
    sapling_calls = int(sum(number(row.get("saplingCallCount")) for row in rows))
    escalation_count = sum(1 for row in rows if bool_value(row.get("usedEscalation")))
    internal_strategies = [number(row.get("internalStrategies")) for row in rows]
    fact_failure_count = sum(
        1
        for row in rows
        if safe_label(row.get("errorCode"), "").lower() == "fact_check_failed"
    )

    daily_counts: dict[str, Counter[str]] = defaultdict(Counter)
    daily_success_cost: dict[str, float] = defaultdict(float)
    daily_success_count: Counter[str] = Counter()
    for row in rows:
        day = day_bucket(row)
        status = safe_label(row.get("status"))
        daily_counts[day][status] += 1
        if status == "success":
            daily_success_count[day] += 1
            daily_success_cost[day] += number(row.get("totalEstimatedCostUsd"))

    daily_cost_per_success = {
        day: daily_success_cost[day] / count
        for day, count in daily_success_count.items()
        if count > 0
    }

    strategy_rows = build_strategy_rows(rows)
    measured_count = len(signal_drops)

    return Analysis(
        period=period,
        total_requests=total_requests,
        status_counts=status_counts,
        failure_counts=failure_counts,
        provider_error_counts=provider_error_counts,
        signal_unavailable_count=signal_unavailable_count,
        measured_count=measured_count,
        average_signal_drop=mean(signal_drops),
        no_regression_rate=percent(no_regression_count, measured_count),
        below_threshold_rate=percent(below_threshold_count, measured_count),
        fact_failure_count=fact_failure_count,
        total_cost=total_cost,
        openai_cost=openai_cost,
        sapling_cost=sapling_cost,
        sapling_calls=sapling_calls,
        cost_per_success=success_cost / len(success_rows) if success_rows else None,
        duration_p50=percentile(durations, 0.5),
        duration_p95=percentile(durations, 0.95),
        escalation_rate=percent(escalation_count, total_requests),
        average_internal_strategies=mean(internal_strategies),
        daily_counts=dict(daily_counts),
        daily_cost_per_success=daily_cost_per_success,
        signal_drops=signal_drops,
        signal_pairs=signal_pairs,
        strategy_rows=strategy_rows,
    )


def build_strategy_rows(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        groups[safe_label(row.get("strategyVersion"))].append(row)

    strategy_rows: list[dict[str, Any]] = []
    for strategy, group in sorted(groups.items()):
        successes = [row for row in group if row.get("status") == "success"]
        quality_failed = [row for row in group if row.get("status") == "quality_failed"]
        drops: list[float] = []
        for row in group:
            draft_signal = optional_number(row.get("draftAiLikePercent"))
            rewrite_signal = optional_number(row.get("rewriteAiLikePercent"))
            if draft_signal is not None and rewrite_signal is not None:
                drops.append(draft_signal - rewrite_signal)
        success_cost = sum(number(row.get("totalEstimatedCostUsd")) for row in successes)
        strategy_rows.append(
            {
                "strategy": strategy,
                "requests": len(group),
                "success_rate": percent(len(successes), len(group)),
                "average_signal_drop": mean(drops),
                "cost_per_success": success_cost / len(successes) if successes else None,
                "quality_failure_rate": percent(len(quality_failed), len(group)),
                "escalation_rate": percent(
                    sum(1 for row in group if bool_value(row.get("usedEscalation"))),
                    len(group),
                ),
            },
        )
    return strategy_rows


def fmt_number(value: float | None, digits: int = 1, suffix: str = "") -> str:
    if value is None:
        return "n/a"
    return f"{value:.{digits}f}{suffix}"


def fmt_percent(value: float | None) -> str:
    return fmt_number(value, 1, "%")


def fmt_money(value: float | None) -> str:
    if value is None:
        return "n/a"
    return f"${value:.4f}"


def fmt_int(value: float | None) -> str:
    if value is None:
        return "n/a"
    return str(int(round(value)))


def top_risks(analysis: Analysis) -> list[str]:
    risks: list[str] = []
    if analysis.total_requests == 0:
        return ["No matching telemetry rows were found for this period."]
    if analysis.signal_unavailable_count:
        risks.append(
            f"{analysis.signal_unavailable_count} requests had unavailable signal scores and were excluded from signal averages.",
        )
    if analysis.no_regression_rate is not None and analysis.no_regression_rate < 95:
        risks.append(
            f"No-regression rate is {fmt_percent(analysis.no_regression_rate)}; inspect candidate selection and quality gates.",
        )
    quality_failed = analysis.status_counts.get("quality_failed", 0)
    quality_rate = percent(quality_failed, analysis.total_requests)
    if quality_rate is not None and quality_rate > 10:
        risks.append(
            f"Quality-failure rate is {fmt_percent(quality_rate)}; review top error labels and strategy routing.",
        )
    if analysis.duration_p95 is not None and analysis.duration_p95 > 30000:
        risks.append(f"Duration p95 is {fmt_int(analysis.duration_p95)} ms; review retry and escalation paths.")
    if analysis.cost_per_success is not None and analysis.cost_per_success > 0.05:
        risks.append(
            f"Cost per successful rewrite is {fmt_money(analysis.cost_per_success)}; review provider budgets.",
        )
    if not risks:
        risks.append("No immediate aggregate quality, cost, or latency risk crossed the report thresholds.")
    return risks


def write_report(path: Path, analysis: Analysis, charts_dir: Path) -> None:
    ensure_parent(path)
    success_count = analysis.status_counts.get("success", 0)
    quality_failed_count = analysis.status_counts.get("quality_failed", 0)
    server_failed_count = analysis.status_counts.get("server_failed", 0)
    success_rate = percent(success_count, analysis.total_requests)
    quality_failed_rate = percent(quality_failed_count, analysis.total_requests)

    lines = [
        "# Rewrite Quality Analysis Report",
        "",
        f"Generated: {dt.datetime.now(dt.timezone.utc).isoformat(timespec='seconds')}",
        f"Period: {analysis.period}",
        "",
        "This internal report uses aggregated `RewriteCostLog` and `RewriteProviderCall` telemetry only. Raw message text, emails, user identifiers, and database URLs are not included.",
        "",
    ]

    if analysis.total_requests == 0:
        lines.extend(
            [
                "## Summary",
                "",
                "No matching rows were found for this period.",
                "",
            ],
        )

    lines.extend(
        [
            "## Key Metrics",
            "",
            "| Metric | Value |",
            "| --- | ---: |",
            f"| Total requests | {analysis.total_requests} |",
            f"| Successful rewrites | {success_count} |",
            f"| Success rate | {fmt_percent(success_rate)} |",
            f"| Quality failures | {quality_failed_count} |",
            f"| Quality-failure rate | {fmt_percent(quality_failed_rate)} |",
            f"| Server failures | {server_failed_count} |",
            f"| Average signal drop | {fmt_number(analysis.average_signal_drop, 1, ' pts')} |",
            f"| Below 50% signal rate | {fmt_percent(analysis.below_threshold_rate)} |",
            f"| No-regression rate | {fmt_percent(analysis.no_regression_rate)} |",
            f"| Signal unavailable rows | {analysis.signal_unavailable_count} |",
            f"| Fact-failure count | {analysis.fact_failure_count} |",
            f"| Total estimated provider cost | {fmt_money(analysis.total_cost)} |",
            f"| Cost per successful rewrite | {fmt_money(analysis.cost_per_success)} |",
            f"| OpenAI-compatible cost | {fmt_money(analysis.openai_cost)} |",
            f"| Sapling calls | {analysis.sapling_calls} |",
            f"| Sapling cost | {fmt_money(analysis.sapling_cost)} |",
            f"| Duration p50 | {fmt_int(analysis.duration_p50)} ms |",
            f"| Duration p95 | {fmt_int(analysis.duration_p95)} ms |",
            f"| Escalation rate | {fmt_percent(analysis.escalation_rate)} |",
            f"| Average internal strategies | {fmt_number(analysis.average_internal_strategies, 2)} |",
            "",
            "## Failure Labels",
            "",
            "| Label | Count |",
            "| --- | ---: |",
        ],
    )

    combined_failures = Counter(analysis.failure_counts)
    combined_failures.update(analysis.provider_error_counts)
    if combined_failures:
        for label, count in combined_failures.most_common():
            lines.append(f"| {label} | {count} |")
    else:
        lines.append("| none | 0 |")

    lines.extend(
        [
            "",
            "## Strategy Version Comparison",
            "",
            "| Strategy version | Requests | Success rate | Avg signal drop | Cost per success | Quality-failure rate | Escalation rate |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: |",
        ],
    )
    if analysis.strategy_rows:
        for row in analysis.strategy_rows:
            lines.append(
                "| {strategy} | {requests} | {success_rate} | {avg_drop} | {cost} | {quality_rate} | {escalation_rate} |".format(
                    strategy=row["strategy"],
                    requests=row["requests"],
                    success_rate=fmt_percent(row["success_rate"]),
                    avg_drop=fmt_number(row["average_signal_drop"], 1, " pts"),
                    cost=fmt_money(row["cost_per_success"]),
                    quality_rate=fmt_percent(row["quality_failure_rate"]),
                    escalation_rate=fmt_percent(row["escalation_rate"]),
                ),
            )
    else:
        lines.append("| none | 0 | n/a | n/a | n/a | n/a | n/a |")

    lines.extend(
        [
            "",
            "## Top Risks And Next Fixes",
            "",
        ],
    )
    for risk in top_risks(analysis):
        lines.append(f"- {risk}")

    lines.extend(
        [
            "",
            "## Charts",
            "",
        ],
    )
    for filename in CHART_FILES.values():
        chart_path = charts_dir / filename
        lines.append(f"- `{chart_path.as_posix()}`")

    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def csv_value(value: float | int | str | None) -> str:
    if value is None:
        return "n/a"
    if isinstance(value, float):
        return f"{value:.6f}".rstrip("0").rstrip(".")
    return str(value)


def write_summary_csv(path: Path, analysis: Analysis) -> None:
    ensure_parent(path)
    rows: list[tuple[str, str, float | int | str | None]] = [
        ("total_requests", "all", analysis.total_requests),
        ("success_count", "all", analysis.status_counts.get("success", 0)),
        ("quality_failed_count", "all", analysis.status_counts.get("quality_failed", 0)),
        ("server_failed_count", "all", analysis.status_counts.get("server_failed", 0)),
        ("success_rate", "all", percent(analysis.status_counts.get("success", 0), analysis.total_requests)),
        ("average_signal_drop", "all", analysis.average_signal_drop),
        ("below_threshold_rate", "all", analysis.below_threshold_rate),
        ("no_regression_rate", "all", analysis.no_regression_rate),
        ("signal_unavailable_count", "all", analysis.signal_unavailable_count),
        ("fact_failure_count", "all", analysis.fact_failure_count),
        ("total_estimated_cost_usd", "all", analysis.total_cost),
        ("cost_per_successful_rewrite_usd", "all", analysis.cost_per_success),
        ("openai_compatible_cost_usd", "all", analysis.openai_cost),
        ("sapling_call_count", "all", analysis.sapling_calls),
        ("sapling_cost_usd", "all", analysis.sapling_cost),
        ("duration_p50_ms", "all", analysis.duration_p50),
        ("duration_p95_ms", "all", analysis.duration_p95),
        ("escalation_rate", "all", analysis.escalation_rate),
        ("average_internal_strategies", "all", analysis.average_internal_strategies),
    ]

    for status in STATUS_ORDER:
        rows.append((f"status_count", status, analysis.status_counts.get(status, 0)))
    for label, count in sorted(analysis.failure_counts.items()):
        rows.append(("failure_label_count", label, count))
    for label, count in sorted(analysis.provider_error_counts.items()):
        rows.append(("provider_error_count", label, count))
    for row in analysis.strategy_rows:
        rows.extend(
            [
                ("strategy_requests", row["strategy"], row["requests"]),
                ("strategy_success_rate", row["strategy"], row["success_rate"]),
                ("strategy_average_signal_drop", row["strategy"], row["average_signal_drop"]),
                ("strategy_cost_per_success_usd", row["strategy"], row["cost_per_success"]),
                ("strategy_quality_failure_rate", row["strategy"], row["quality_failure_rate"]),
                ("strategy_escalation_rate", row["strategy"], row["escalation_rate"]),
            ],
        )

    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["metric", "segment", "value", "period"])
        for metric, segment, value in rows:
            writer.writerow([metric, segment, csv_value(value), analysis.period])


Color = Tuple[int, int, int]


class Canvas:
    def __init__(self, width: int = 900, height: int = 520):
        self.width = width
        self.height = height
        self.pixels = bytearray([247, 248, 250] * width * height)

    def set_pixel(self, x: int, y: int, color: Color) -> None:
        if x < 0 or y < 0 or x >= self.width or y >= self.height:
            return
        offset = (y * self.width + x) * 3
        self.pixels[offset:offset + 3] = bytes(color)

    def rect(self, x0: int, y0: int, x1: int, y1: int, color: Color) -> None:
        left, right = sorted((max(0, x0), min(self.width - 1, x1)))
        top, bottom = sorted((max(0, y0), min(self.height - 1, y1)))
        for y in range(top, bottom + 1):
            for x in range(left, right + 1):
                self.set_pixel(x, y, color)

    def line(self, x0: int, y0: int, x1: int, y1: int, color: Color) -> None:
        dx = abs(x1 - x0)
        dy = -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        x, y = x0, y0
        while True:
            self.set_pixel(x, y, color)
            if x == x1 and y == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x += sx
            if e2 <= dx:
                err += dx
                y += sy

    def circle(self, cx: int, cy: int, radius: int, color: Color) -> None:
        for y in range(cy - radius, cy + radius + 1):
            for x in range(cx - radius, cx + radius + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= radius ** 2:
                    self.set_pixel(x, y, color)

    def save(self, path: Path) -> None:
        ensure_parent(path)
        raw = bytearray()
        row_width = self.width * 3
        for y in range(self.height):
            raw.append(0)
            start = y * row_width
            raw.extend(self.pixels[start:start + row_width])
        png = bytearray()
        png.extend(b"\x89PNG\r\n\x1a\n")

        def chunk(kind: bytes, data: bytes) -> None:
            png.extend(struct.pack(">I", len(data)))
            png.extend(kind)
            png.extend(data)
            png.extend(struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF))

        chunk(b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 2, 0, 0, 0))
        chunk(b"IDAT", zlib.compress(bytes(raw), level=9))
        chunk(b"IEND", b"")
        path.write_bytes(bytes(png))


def plot_area() -> tuple[int, int, int, int]:
    return 70, 40, 850, 450


def draw_axes(canvas: Canvas) -> tuple[int, int, int, int]:
    left, top, right, bottom = plot_area()
    axis = (82, 94, 111)
    grid = (226, 232, 240)
    for step in range(6):
        y = bottom - round((bottom - top) * step / 5)
        canvas.line(left, y, right, y, grid)
    canvas.line(left, top, left, bottom, axis)
    canvas.line(left, bottom, right, bottom, axis)
    return left, top, right, bottom


def write_bar_chart(path: Path, values: list[float]) -> None:
    canvas = Canvas()
    left, top, right, bottom = draw_axes(canvas)
    colors: list[Color] = [
        (37, 99, 235),
        (22, 163, 74),
        (220, 38, 38),
        (234, 88, 12),
        (124, 58, 237),
        (8, 145, 178),
    ]
    if not values:
        canvas.save(path)
        return
    max_value = max(max(values), 1)
    gap = 18
    width = max(8, ((right - left) - gap * (len(values) + 1)) // len(values))
    for index, value in enumerate(values):
        height = round((bottom - top) * (value / max_value))
        x0 = left + gap + index * (width + gap)
        canvas.rect(x0, bottom - height, x0 + width, bottom, colors[index % len(colors)])
    canvas.save(path)


def write_line_chart(path: Path, values: list[float]) -> None:
    canvas = Canvas()
    left, top, right, bottom = draw_axes(canvas)
    if not values:
        canvas.save(path)
        return
    max_value = max(max(values), 1)
    points: list[tuple[int, int]] = []
    for index, value in enumerate(values):
        x = left if len(values) == 1 else left + round((right - left) * index / (len(values) - 1))
        y = bottom - round((bottom - top) * (value / max_value))
        points.append((x, y))
    for start, end in zip(points, points[1:]):
        canvas.line(start[0], start[1], end[0], end[1], (37, 99, 235))
    for x, y in points:
        canvas.circle(x, y, 5, (37, 99, 235))
    canvas.save(path)


def write_histogram(path: Path, values: list[float]) -> None:
    bins = [-50, -25, 0, 25, 50, 75, 100]
    counts = [0 for _ in bins]
    for value in values:
        placed = False
        for index, upper in enumerate(bins):
            if value <= upper:
                counts[index] += 1
                placed = True
                break
        if not placed:
            counts[-1] += 1
    write_bar_chart(path, [float(value) for value in counts])


def write_scatter_chart(path: Path, pairs: list[tuple[float, float]]) -> None:
    canvas = Canvas()
    left, top, right, bottom = draw_axes(canvas)
    canvas.line(left, bottom, right, top, (148, 163, 184))
    for draft_signal, rewrite_signal in pairs:
        x = left + round((right - left) * max(0, min(100, draft_signal)) / 100)
        y = bottom - round((bottom - top) * max(0, min(100, rewrite_signal)) / 100)
        color = (22, 163, 74) if rewrite_signal <= draft_signal else (220, 38, 38)
        canvas.circle(x, y, 5, color)
    canvas.save(path)


def write_charts(charts_dir: Path, analysis: Analysis) -> None:
    charts_dir.mkdir(parents=True, exist_ok=True)
    days = sorted(analysis.daily_counts)
    daily_values = [float(sum(analysis.daily_counts[day].values())) for day in days]
    write_bar_chart(charts_dir / CHART_FILES["daily_volume"], daily_values)
    write_bar_chart(
        charts_dir / CHART_FILES["status_breakdown"],
        [float(analysis.status_counts.get(status, 0)) for status in STATUS_ORDER],
    )
    write_histogram(charts_dir / CHART_FILES["signal_distribution"], analysis.signal_drops)
    write_scatter_chart(charts_dir / CHART_FILES["signal_scatter"], analysis.signal_pairs)
    write_line_chart(
        charts_dir / CHART_FILES["cost_over_time"],
        [analysis.daily_cost_per_success[day] for day in sorted(analysis.daily_cost_per_success)],
    )
    write_bar_chart(
        charts_dir / CHART_FILES["strategy_comparison"],
        [
            number(row["success_rate"])
            for row in analysis.strategy_rows
            if row["success_rate"] is not None
        ],
    )


def period_label(since: str | None, until: str | None) -> str:
    return f"{since or 'beginning'}..{until or 'now'}"


def main() -> int:
    args = parse_args()
    since = validate_date(args.since, "--since")
    until = validate_date(args.until, "--until")
    if since and until and since >= until:
        raise AnalysisError("--since must be earlier than --until.")

    database_url = read_database_url(args.database_url_env)
    output_path = Path(args.output)
    summary_csv_path = Path(args.summary_csv)
    charts_dir = Path(args.charts_dir)
    query_result = fetch_telemetry(database_url, since, until)
    analysis = analyze(
        query_result.rows,
        query_result.provider_rows,
        period_label(since, until),
    )
    write_charts(charts_dir, analysis)
    write_report(output_path, analysis, charts_dir)
    write_summary_csv(summary_csv_path, analysis)
    print(output_path.as_posix())
    print(summary_csv_path.as_posix())
    print(charts_dir.as_posix())
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AnalysisError as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(2)
