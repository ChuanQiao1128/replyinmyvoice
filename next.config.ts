import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  distDir: process.env.NEXT_DIST_DIR || ".next",
  poweredByHeader: false,
  async redirects() {
    // Retired routes from the old Student/Exam pricing era.
    return [
      { source: "/students", destination: "/", permanent: true },
      { source: "/launch", destination: "/", permanent: true },
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
