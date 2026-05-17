import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  poweredByHeader: false,
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
