export type ScenarioCaseResult = {
  customerUsablePass?: boolean;
  strictSignalPass?: boolean;
};

export type ParsedScenarioEvaluationResults = {
  averageSignalReductionPoints: number;
  below50Percent: number;
  cases: Map<string, ScenarioCaseResult>;
};

export type ScenarioEvaluationComparison = {
  passed: boolean;
  failures: string[];
  summary: {
    averageSignalReductionDrop: number;
    below50Drop: number;
  };
};

type CompareInput = {
  baseMarkdown: string;
  candidateMarkdown: string;
  averageDropThresholdPoints?: number;
  below50DropThresholdPoints?: number;
};

const DEFAULT_REGRESSION_THRESHOLD_POINTS = 5;
const FLOAT_TOLERANCE = 0.000001;

export function parseScenarioEvaluationResults(
  markdown: string,
): ParsedScenarioEvaluationResults {
  const averageSignalReductionPoints = parseAverageSignalReduction(markdown);
  const below50Percent = parseBelow50Percent(markdown);
  const cases = parseCases(markdown);

  return {
    averageSignalReductionPoints,
    below50Percent,
    cases,
  };
}

export function compareScenarioEvaluationResults({
  baseMarkdown,
  candidateMarkdown,
  averageDropThresholdPoints = DEFAULT_REGRESSION_THRESHOLD_POINTS,
  below50DropThresholdPoints = DEFAULT_REGRESSION_THRESHOLD_POINTS,
}: CompareInput): ScenarioEvaluationComparison {
  const base = parseScenarioEvaluationResults(baseMarkdown);
  const candidate = parseScenarioEvaluationResults(candidateMarkdown);
  const averageSignalReductionDrop =
    base.averageSignalReductionPoints - candidate.averageSignalReductionPoints;
  const below50Drop = base.below50Percent - candidate.below50Percent;
  const failures: string[] = [];

  if (averageSignalReductionDrop >= averageDropThresholdPoints - FLOAT_TOLERANCE) {
    failures.push(
      `Average signal reduction dropped by ${formatPoints(
        averageSignalReductionDrop,
      )} points: main ${formatPoints(
        base.averageSignalReductionPoints,
      )}, candidate ${formatPoints(candidate.averageSignalReductionPoints)}.`,
    );
  }

  if (below50Drop >= below50DropThresholdPoints - FLOAT_TOLERANCE) {
    failures.push(
      `Below-50 rate dropped by ${formatPoints(
        below50Drop,
      )} points: main ${formatPoints(base.below50Percent)}%, candidate ${formatPoints(
        candidate.below50Percent,
      )}%.`,
    );
  }

  for (const [caseId, baseCase] of base.cases) {
    const candidateCase = candidate.cases.get(caseId);

    addPassRegressionFailure({
      failures,
      caseId,
      label: "Customer-usable pass",
      baseValue: baseCase.customerUsablePass,
      candidateValue: candidateCase?.customerUsablePass,
    });
    addPassRegressionFailure({
      failures,
      caseId,
      label: "Strict signal pass",
      baseValue: baseCase.strictSignalPass,
      candidateValue: candidateCase?.strictSignalPass,
    });
  }

  return {
    passed: failures.length === 0,
    failures,
    summary: {
      averageSignalReductionDrop,
      below50Drop,
    },
  };
}

function parseAverageSignalReduction(markdown: string): number {
  const match = markdown.match(
    /^Average AI-like signal (?:drop|reduction):\s*(-?\d+(?:\.\d+)?)\s*(?:pts?|points?)\s*$/im,
  );

  if (!match) {
    throw new Error("Missing average AI-like signal reduction metric.");
  }

  return Number(match[1]);
}

function parseBelow50Percent(markdown: string): number {
  const match = markdown.match(
    /^Rewrite(?:s)? below 50% AI-like signal:\s*(\d+)\s*\/\s*(\d+)\s*$/im,
  );

  if (!match) {
    throw new Error("Missing rewrite below-50 AI-like signal metric.");
  }

  const below50Count = Number(match[1]);
  const measuredCount = Number(match[2]);

  if (measuredCount <= 0) {
    throw new Error("Rewrite below-50 AI-like signal metric has zero total cases.");
  }

  return (below50Count / measuredCount) * 100;
}

function parseCases(markdown: string): Map<string, ScenarioCaseResult> {
  const headings = [...markdown.matchAll(/^##\s+(.+?)\s*$/gm)];
  const cases = new Map<string, ScenarioCaseResult>();

  headings.forEach((heading, index) => {
    const caseId = heading[1].trim();
    const sectionStart = (heading.index ?? 0) + heading[0].length;
    const nextHeading = headings[index + 1];
    const sectionEnd = nextHeading?.index ?? markdown.length;
    const section = markdown.slice(sectionStart, sectionEnd);

    cases.set(caseId, {
      customerUsablePass: parseYesNoField(section, "Customer-usable pass"),
      strictSignalPass: parseYesNoField(section, "Strict signal pass"),
    });
  });

  return cases;
}

function parseYesNoField(section: string, label: string): boolean | undefined {
  const escapedLabel = label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const match = section.match(new RegExp(`^${escapedLabel}:\\s*(yes|no)\\s*$`, "im"));

  if (!match) {
    return undefined;
  }

  return match[1].toLowerCase() === "yes";
}

function addPassRegressionFailure({
  failures,
  caseId,
  label,
  baseValue,
  candidateValue,
}: {
  failures: string[];
  caseId: string;
  label: string;
  baseValue: boolean | undefined;
  candidateValue: boolean | undefined;
}): void {
  if (baseValue !== true) {
    return;
  }

  if (candidateValue === false) {
    failures.push(`Case ${caseId} regressed: ${label} changed from yes to no.`);
    return;
  }

  if (candidateValue !== true) {
    failures.push(`Case ${caseId} regressed: ${label} was yes on main but is missing.`);
  }
}

function formatPoints(value: number): string {
  return value.toFixed(2);
}
