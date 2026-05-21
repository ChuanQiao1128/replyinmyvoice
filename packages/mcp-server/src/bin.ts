#!/usr/bin/env node
import { runStdio } from "./index.js";

runStdio().catch((err) => {
  console.error("[replyinmyvoice-mcp] fatal:", err);
  process.exit(1);
});
