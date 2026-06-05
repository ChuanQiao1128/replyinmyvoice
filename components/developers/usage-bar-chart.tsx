"use client";

import { useMemo, useState } from "react";

export type UsageSeriesPoint = {
  calls: number;
  date: string;
  failed: number;
  succeeded: number;
};

type UsageBarChartProps = {
  points: UsageSeriesPoint[];
};

const width = 820;
const height = 280;
const padding = {
  bottom: 34,
  left: 36,
  right: 18,
  top: 26,
};

function formatShortDate(value: string) {
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "short",
  }).format(date);
}

function formatFullDate(value: string) {
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(date);
}

function safeCount(value: number) {
  return Number.isFinite(value) && value > 0 ? value : 0;
}

export function UsageBarChart({ points }: UsageBarChartProps) {
  const [activeIndex, setActiveIndex] = useState<number | null>(null);

  const chartPoints = useMemo(
    () =>
      points.map((point) => ({
        calls: safeCount(point.calls),
        date: point.date,
        failed: safeCount(point.failed),
        succeeded: safeCount(point.succeeded),
      })),
    [points],
  );

  const maxCalls = Math.max(1, ...chartPoints.map((point) => point.calls));
  const plotWidth = width - padding.left - padding.right;
  const plotHeight = height - padding.top - padding.bottom;
  const slotWidth = plotWidth / Math.max(chartPoints.length, 1);
  const barWidth = Math.max(8, Math.min(18, slotWidth * 0.58));
  const activePoint =
    activeIndex === null ? chartPoints.at(-1) : chartPoints[activeIndex];

  return (
    <div className="rounded-lg border border-line bg-white/75 p-4 sm:p-5">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h3 className="text-xl">30-day API calls</h3>
          <p className="mt-1 text-sm text-ink/60">
            Daily calls across all active and historical keys.
          </p>
        </div>
        <div className="rounded-md border border-line bg-paper px-3 py-2 text-sm text-ink/70">
          {activePoint ? (
            <span>
              <span className="font-semibold text-ink">
                {formatFullDate(activePoint.date)}
              </span>
              {": "}
              {activePoint.calls} calls, {activePoint.succeeded} succeeded,{" "}
              {activePoint.failed} failed
            </span>
          ) : (
            <span>No calls yet</span>
          )}
        </div>
      </div>

      <div className="mt-5 overflow-x-auto">
        <svg
          aria-label="30-day API usage bar chart"
          className="min-w-[720px]"
          height={height}
          role="img"
          viewBox={`0 0 ${width} ${height}`}
          width="100%"
        >
          <title>30-day API usage by day</title>
          <line
            stroke="rgba(18, 22, 14, 0.18)"
            strokeWidth="1"
            x1={padding.left}
            x2={width - padding.right}
            y1={height - padding.bottom}
            y2={height - padding.bottom}
          />
          {[0.25, 0.5, 0.75, 1].map((tick) => {
            const y = padding.top + plotHeight * (1 - tick);
            return (
              <line
                key={tick}
                stroke="rgba(18, 22, 14, 0.08)"
                strokeWidth="1"
                x1={padding.left}
                x2={width - padding.right}
                y1={y}
                y2={y}
              />
            );
          })}
          {chartPoints.map((point, index) => {
            const x = padding.left + index * slotWidth + (slotWidth - barWidth) / 2;
            const callsHeight = (point.calls / maxCalls) * plotHeight;
            const succeededHeight =
              point.calls === 0 ? 0 : (point.succeeded / point.calls) * callsHeight;
            const failedHeight = Math.max(0, callsHeight - succeededHeight);
            const visibleSucceededHeight =
              point.succeeded > 0 ? succeededHeight : 0;
            const y = height - padding.bottom - callsHeight;
            const label = `${formatFullDate(point.date)}: ${point.calls} calls, ${point.succeeded} succeeded, ${point.failed} failed`;
            const active = activeIndex === index;

            return (
              <g key={point.date}>
                <rect
                  aria-label={label}
                  fill="transparent"
                  height={plotHeight}
                  onBlur={() => setActiveIndex(null)}
                  onFocus={() => setActiveIndex(index)}
                  onMouseEnter={() => setActiveIndex(index)}
                  onMouseLeave={() => setActiveIndex(null)}
                  role="graphics-symbol"
                  tabIndex={0}
                  width={Math.max(slotWidth, 12)}
                  x={padding.left + index * slotWidth}
                  y={padding.top}
                >
                  <title>{label}</title>
                </rect>
                <rect
                  fill={active ? "#d98a4a" : "#e3b46e"}
                  height={visibleSucceededHeight}
                  pointerEvents="none"
                  rx="3"
                  width={barWidth}
                  x={x}
                  y={height - padding.bottom - visibleSucceededHeight}
                />
                {failedHeight > 0 ? (
                  <rect
                    fill={active ? "#b5481b" : "#c2571c"}
                    height={failedHeight}
                    pointerEvents="none"
                    rx="3"
                    width={barWidth}
                    x={x}
                    y={y}
                  />
                ) : null}
                {index % 5 === 0 || index === chartPoints.length - 1 ? (
                  <text
                    fill="rgba(18, 22, 14, 0.5)"
                    fontSize="11"
                    textAnchor="middle"
                    x={x + barWidth / 2}
                    y={height - 10}
                  >
                    {formatShortDate(point.date)}
                  </text>
                ) : null}
              </g>
            );
          })}
        </svg>
      </div>

      <div className="mt-3 flex flex-wrap gap-4 text-xs text-ink/55">
        <span className="inline-flex items-center gap-2">
          <span className="h-2.5 w-2.5 rounded-sm bg-[#e3b46e]" />
          Succeeded
        </span>
        <span className="inline-flex items-center gap-2">
          <span className="h-2.5 w-2.5 rounded-sm bg-rust" />
          Failed
        </span>
      </div>
    </div>
  );
}
