import { rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

await rm(path.join(root, ".open-next"), {
  force: true,
  recursive: true,
});
