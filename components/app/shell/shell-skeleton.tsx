import type { CSSProperties, ReactNode } from "react";
import Link from "next/link";

import { AppSidebar } from "./app-sidebar";
import { ShellIcon } from "./shell-icons";
import { SectionCard, Skeleton } from "./shell-primitives";
import styles from "./shell.module.css";

function cx(...names: Array<string | false | null | undefined>) {
  return names.filter(Boolean).join(" ");
}

function SkeletonBlock({
  className,
  style,
}: {
  className?: string;
  style?: CSSProperties;
}) {
  return (
    <span
      aria-hidden="true"
      className={cx(styles.skeletonBlock, className)}
      style={style}
    />
  );
}

export function AppShellLoadingFrame({ children }: { children: ReactNode }) {
  return (
    <div className={styles.shell} aria-busy="true">
      <header className={styles.topbar}>
        <div className={styles.topbarInner}>
          <button
            type="button"
            className={styles.hamburger}
            aria-label="Menu loading"
            disabled
          >
            <ShellIcon name="menu" size={20} />
          </button>
          <Link href="/app" className={styles.brand}>
            <span className={styles.brandMark} aria-hidden="true">
              R
            </span>
            <span className={styles.brandText}>Reply In My Voice</span>
          </Link>
          <div className={styles.topbarRight}>
            <div
              className={cx(styles.quotaPill, styles.shellSkeletonPill)}
              aria-hidden="true"
            >
              <SkeletonBlock className={styles.shellSkeletonQuotaTop} />
              <SkeletonBlock className={styles.shellSkeletonQuotaTrack} />
            </div>
            <SkeletonBlock className={styles.shellSkeletonDocs} />
            <div className={styles.accountBtn} aria-hidden="true">
              <SkeletonBlock className={styles.shellSkeletonAvatar} />
              <SkeletonBlock className={styles.shellSkeletonTiny} />
            </div>
          </div>
        </div>
      </header>

      <div className={styles.body}>
        <AppSidebar />
        <main className={styles.main}>
          <div className={styles.mainInner}>{children}</div>
        </main>
      </div>
    </div>
  );
}

export function LoadingPageHeader({
  actionWidth,
  descriptionWidth = "520px",
  titleWidth = "180px",
}: {
  actionWidth?: string;
  descriptionWidth?: string;
  titleWidth?: string;
}) {
  const header = (
    <header className={styles.pageHeader} aria-hidden="true">
      <SkeletonBlock
        className={styles.loadingTitle}
        style={{ width: titleWidth }}
      />
      <SkeletonBlock
        className={styles.loadingDescription}
        style={{ marginTop: 12, width: descriptionWidth }}
      />
    </header>
  );

  if (!actionWidth) {
    return header;
  }

  return (
    <div className={styles.pageHeaderRow} aria-hidden="true">
      {header}
      <SkeletonBlock
        className={styles.loadingAction}
        style={{ width: actionWidth }}
      />
    </div>
  );
}

export function WorkspaceLoadingSkeleton() {
  return (
    <div className={styles.loadingStack}>
      <LoadingPageHeader
        titleWidth="132px"
        descriptionWidth="620px"
        actionWidth="118px"
      />
      <SectionCard>
        <Skeleton lines={2} />
      </SectionCard>
      <div className={styles.loadingWorkspaceGrid} aria-hidden="true">
        <section className={styles.loadingPane}>
          <div className={styles.loadingToolbar}>
            <SkeletonBlock style={{ height: 11, width: 96 }} />
            <SkeletonBlock style={{ height: 11, width: 72 }} />
          </div>
          <SkeletonBlock className={styles.loadingTextarea} />
          <div className={styles.loadingFooter}>
            <SkeletonBlock style={{ height: 12, width: 210 }} />
            <SkeletonBlock
              className={styles.loadingAction}
              style={{ width: 116 }}
            />
          </div>
          <SkeletonBlock style={{ height: 12, marginTop: 18, width: "72%" }} />
        </section>
        <section className={styles.loadingPane}>
          <div className={styles.loadingToolbar}>
            <SkeletonBlock style={{ height: 11, width: 112 }} />
            <div style={{ display: "flex", gap: 10 }}>
              <SkeletonBlock
                className={styles.loadingAction}
                style={{ width: 86 }}
              />
              <SkeletonBlock
                className={styles.loadingAction}
                style={{ width: 86 }}
              />
            </div>
          </div>
          <SkeletonBlock className={styles.loadingOutput} />
        </section>
      </div>
    </div>
  );
}

export function HistoryLoadingSkeleton() {
  return (
    <>
      <LoadingPageHeader titleWidth="112px" descriptionWidth="470px" />
      <div className={styles.list}>
        <Skeleton lines={2} />
        <Skeleton lines={2} />
        <Skeleton lines={2} />
      </div>
    </>
  );
}

export function AccountLoadingSkeleton() {
  return (
    <div className={styles.loadingStack}>
      <LoadingPageHeader
        titleWidth="118px"
        descriptionWidth="360px"
        actionWidth="104px"
      />
      <div className={styles.statGrid} aria-hidden="true">
        {Array.from({ length: 4 }).map((_, index) => (
          <div className={styles.statCard} key={index}>
            <SkeletonBlock style={{ height: 11, width: 92 }} />
            <SkeletonBlock style={{ height: 20, marginTop: 12, width: "72%" }} />
            <SkeletonBlock style={{ height: 12, marginTop: 10, width: "58%" }} />
          </div>
        ))}
      </div>
      <SectionCard>
        <div className={styles.cardHead}>
          <div>
            <SkeletonBlock style={{ height: 20, width: 160 }} />
            <SkeletonBlock style={{ height: 13, marginTop: 10, width: 260 }} />
          </div>
        </div>
        <Skeleton lines={4} />
      </SectionCard>
      <SectionCard>
        <div className={styles.cardHead}>
          <div>
            <SkeletonBlock style={{ height: 20, width: 140 }} />
            <SkeletonBlock style={{ height: 13, marginTop: 10, width: 320 }} />
          </div>
        </div>
        <div className={styles.loadingCardStack} aria-hidden="true">
          <SkeletonBlock style={{ height: 42, borderRadius: 9 }} />
          <SkeletonBlock style={{ height: 96, borderRadius: 9 }} />
          <SkeletonBlock
            className={styles.loadingAction}
            style={{ width: 128 }}
          />
        </div>
      </SectionCard>
    </div>
  );
}

function DeveloperSummaryCards() {
  return (
    <div className={styles.loadingTabGrid} aria-hidden="true">
      {Array.from({ length: 3 }).map((_, index) => (
        <div className={styles.statCard} key={index}>
          <SkeletonBlock style={{ height: 14, width: 74 }} />
          <SkeletonBlock style={{ height: 12, marginTop: 10, width: "82%" }} />
        </div>
      ))}
    </div>
  );
}

export function DeveloperLoadingSkeleton({
  descriptionWidth = "500px",
  titleWidth = "118px",
}: {
  descriptionWidth?: string;
  titleWidth?: string;
}) {
  return (
    <div className={styles.loadingStack}>
      <LoadingPageHeader
        titleWidth={titleWidth}
        descriptionWidth={descriptionWidth}
      />
      <SectionCard>
        <div className={styles.loadingDeveloperHero} aria-hidden="true">
          <div>
            <SkeletonBlock style={{ height: 12, width: 150 }} />
            <SkeletonBlock style={{ height: 42, marginTop: 16, width: "62%" }} />
            <SkeletonBlock style={{ height: 15, marginTop: 14, width: "74%" }} />
          </div>
          <div style={{ display: "grid", gap: 8 }}>
            <SkeletonBlock style={{ height: 44, borderRadius: 8 }} />
            <SkeletonBlock style={{ height: 44, borderRadius: 8 }} />
            <SkeletonBlock style={{ height: 44, borderRadius: 8 }} />
          </div>
        </div>
        <div style={{ height: 18 }} />
        <DeveloperSummaryCards />
      </SectionCard>
      <SectionCard>
        <Skeleton lines={5} />
      </SectionCard>
    </div>
  );
}

export function ConnectLoadingSkeleton() {
  return (
    <div className={styles.loadingStack}>
      <LoadingPageHeader titleWidth="120px" descriptionWidth="680px" />
      <div className={styles.calloutRow} aria-hidden="true">
        <SkeletonBlock style={{ height: 14, width: 260 }} />
        <SkeletonBlock className={styles.loadingAction} style={{ width: 112 }} />
        <SkeletonBlock className={styles.loadingAction} style={{ width: 132 }} />
      </div>
      {Array.from({ length: 3 }).map((_, index) => (
        <div className={styles.loadingConfigCard} key={index}>
          <SkeletonBlock style={{ height: 18, width: 210 }} />
          <SkeletonBlock className={styles.loadingCodeBlock} />
        </div>
      ))}
    </div>
  );
}
