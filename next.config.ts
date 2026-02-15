import type { NextConfig } from "next";

const nextConfig: NextConfig = {
images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "**.**.**",
        port: "",
      },
      {
        protocol: "https",
        hostname: "**.**.**.**",
        port: "",
        pathname: "/**",
      },
      {
        protocol: "http",
        hostname: "**.**.**.**",
        port: "",
      },
      {
        protocol: "https",
        hostname: "**",
        port: "",
      },
      {
        protocol: "http",
        hostname: "**",
        port: "",
      },
      {
        protocol: "http",
        hostname: "**.**.**",
        port: "",
      },
    ],
  },
  typescript: {
    ignoreBuildErrors: true,
  },
};

export default nextConfig;
