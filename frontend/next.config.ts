import type { NextConfig } from "next";

// Where the Next.js server forwards /api/v1/*. Read at build time, so the Docker image
// gets it as a build argument.
const apiUrl = process.env.API_URL ?? "http://localhost:5243";

const nextConfig: NextConfig = {
  output: "standalone",
  cacheComponents: true,
  partialPrefetching: true,
  // Proxying the API keeps it on the frontend's origin: the refresh token cookie
  // (HttpOnly, SameSite=Strict) then works without cross-origin requests
  rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${apiUrl}/api/v1/:path*`,
      },
    ];
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
