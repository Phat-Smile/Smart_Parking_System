import type { NextConfig } from "next";

const gatewayUrl =
    process.env.NEXT_PUBLIC_API_BASE_URL?.trim() || "http://localhost:5000";

const nextConfig: NextConfig = {
    output: "standalone",
    async rewrites() {
        return [
            {
                source: "/api/payment/:path*",
                destination: `${gatewayUrl}/api/payment/:path*`,
            },
        ];
    },
};

export default nextConfig;
