import { NextResponse } from "next/server";
import { exec } from "child_process";
import { promisify } from "util";

const execAsync = promisify(exec);

export async function GET(request: Request) {
    const { searchParams } = new URL(request.url);
    const targetEmail = (searchParams.get("email") || "").trim().toLowerCase();

    try {
        // Read the latest 40 log lines from notification container
        const { stdout, stderr } = await execAsync(
            "docker logs --tail 40 parking-system-notification-api-1",
        );
        const raw = (stdout || "") + "\n" + (stderr || "");
        const lines = raw.split("\n");

        // Scan backwards (from latest to oldest)
        for (const line of lines.reverse()) {
            const trimmed = line.trim();
            if (!trimmed.startsWith("{")) continue;

            try {
                const json = JSON.parse(trimmed);
                const email = json?.State?.Email?.toString()?.toLowerCase();
                const otpCode = json?.State?.OtpCode?.toString();

                if (otpCode && /^[0-9]{6}$/.test(otpCode)) {
                    // If a specific email was requested, match it; otherwise take the latest
                    if (
                        !targetEmail ||
                        (email && email.includes(targetEmail))
                    ) {
                        return NextResponse.json({
                            email: json?.State?.Email || targetEmail,
                            otpCode,
                            found: true,
                            source: "notification_container",
                        });
                    }
                }
            } catch {
                // skip non-json line
            }
        }
    } catch (err: any) {
        // Docker command not available in this environment
    }

    return NextResponse.json({
        email: targetEmail,
        otpCode: null,
        found: false,
    });
}
