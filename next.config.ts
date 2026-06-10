import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  distDir: process.env.NEXT_DIST_DIR || ".next",
  poweredByHeader: false,
  async redirects() {
    return [
      // Retired routes from the old Student/Exam pricing era.
      { source: "/students", destination: "/", permanent: true },
      { source: "/launch", destination: "/", permanent: true },
      // The developer console now lives inside the signed-in app shell.
      { source: "/developers/keys", destination: "/app/keys", permanent: true },
    ];
  },
  webpack(config) {
    config.experiments = {
      ...config.experiments,
      asyncWebAssembly: true,
    };
    config.module.rules.push({
      test: /\.wasm$/i,
      type: "webassembly/async",
    });

    return config;
  },
};

export default nextConfig;
