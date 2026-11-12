"use client";

import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import Script from "next/script";
import { getGoogleClientId } from "@/shared/config/env";

const GIS_SCRIPT = "https://accounts.google.com/gsi/client";

/**
 * Google's own sign-in button (Google Identity Services). It hands back an ID token, which Identity verifies.
 * Without NEXT_PUBLIC_GOOGLE_CLIENT_ID the ParkWise button is kept and only reports that Google is unavailable.
 */
export function GoogleSignInButton({
    text,
    onCredential,
    onUnavailable,
}: {
    text: "continue_with" | "signup_with";
    onCredential: (idToken: string) => void;
    onUnavailable: () => void;
}) {
    const clientId = getGoogleClientId();
    const containerRef = useRef<HTMLDivElement>(null);
    const onCredentialRef = useRef(onCredential);
    const [scriptReady, setScriptReady] = useState(false);

    useEffect(() => {
        onCredentialRef.current = onCredential;
    }, [onCredential]);

    useEffect(() => {
        const container = containerRef.current;
        const google = window.google;
        if (!clientId || !scriptReady || !container || !google) return;

        google.accounts.id.initialize({
            client_id: clientId,
            callback: (response) =>
                onCredentialRef.current(response.credential),
            ux_mode: "popup",
        });
        let renderedWidth = 0;
        const renderAtContainerWidth = () => {
            // GIS takes a pixel width. Re-render when the form changes width.
            const width = Math.min(400, Math.max(200, container.clientWidth));
            if (width === renderedWidth) return;
            renderedWidth = width;
            container.replaceChildren();
            google.accounts.id.renderButton(container, {
                type: "standard",
                theme: "outline",
                // Medium avoids the personalized account card between visits.
                size: "medium",
                shape: "rectangular",
                text,
                logo_alignment: "center",
                locale: "en",
                width,
            });
        };

        renderAtContainerWidth();
        if (typeof ResizeObserver === "undefined") return;
        const observer = new ResizeObserver(renderAtContainerWidth);
        observer.observe(container);
        return () => observer.disconnect();
    }, [clientId, scriptReady, text]);

    if (!clientId) {
        return (
            <button
                className="parkwise-login-google"
                type="button"
                onClick={onUnavailable}
            >
                <Image
                    src="/images/parkwise/google.png"
                    alt=""
                    width={22}
                    height={22}
                />
                <span>Continue with Google</span>
            </button>
        );
    }

    return (
        <>
            <Script
                src={GIS_SCRIPT}
                strategy="afterInteractive"
                onReady={() => setScriptReady(true)}
                onError={onUnavailable}
            />
            <div
                ref={containerRef}
                className="parkwise-google-gis"
                data-testid="google-sign-in"
            />
        </>
    );
}
