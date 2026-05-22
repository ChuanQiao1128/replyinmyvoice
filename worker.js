import openNextWorker, {
  BucketCachePurge,
  DOQueueHandler,
  DOShardedTagCache,
} from "./.open-next/worker.js";
import { runScheduledLearningOps } from "./lib/learningops/scheduled.ts";

export { BucketCachePurge, DOQueueHandler, DOShardedTagCache };

const worker = {
  fetch(request, env, ctx) {
    return openNextWorker.fetch(request, env, ctx);
  },

  scheduled(_controller, env, ctx) {
    ctx.waitUntil(
      runScheduledLearningOps({ env }).catch((error) => {
        console.error(
          "learningops_cron_failed",
          error instanceof Error ? error.message : "unknown_error",
        );
      }),
    );
  },
};

export default worker;
