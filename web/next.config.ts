import type { NextConfig } from "next";

const gatewayUrl =
    process.env.NEXT_PUBLIC_API_BASE_URL?.trim() || "http://localhost:5000";

// QA-only proxy for Parking. Preserve the develop Payment route too.
const parkingQaApiUrl = process.env.PARKING_QA_API_URL?.trim().replace(
    /\/$/,
    "",
);

const nextConfig: NextConfig = {
    output: "standalone",
    experimental: {
        cpus: 1,
    },
    async rewrites() {
        return [
            {
                source: "/api/payment/:path*",
                destination: `${gatewayUrl}/api/payment/:path*`,
            },
            ...(parkingQaApiUrl
                ? [
                      {
                          source: "/api/parking/:path*",
                          destination: `${parkingQaApiUrl}/api/parking/:path*`,
                      },
                  ]
                : []),
        ];
    },
};

export default nextConfig;
