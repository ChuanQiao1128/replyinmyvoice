import {
  AppShellLoadingFrame,
  WorkspaceLoadingSkeleton,
} from "../../components/app/shell/shell-skeleton";

export default function AppLoading() {
  return (
    <AppShellLoadingFrame>
      <WorkspaceLoadingSkeleton />
    </AppShellLoadingFrame>
  );
}
