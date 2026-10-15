"use client";

import dynamic from "next/dynamic";
import {
    Component,
    useCallback,
    useEffect,
    useState,
    type ReactNode,
} from "react";
import type { DestinationScrollController } from "./useParkWiseLandingMotion";

const DestinationParkingScene = dynamic(
    () => import("./DestinationParkingScene"),
    { ssr: false, loading: () => null },
);

function supportsWebGL() {
    try {
        const canvas = document.createElement("canvas");
        const context =
            canvas.getContext("webgl2") ?? canvas.getContext("webgl");
        context?.getExtension("WEBGL_lose_context")?.loseContext();
        return Boolean(context);
    } catch {
        return false;
    }
}

class SceneErrorBoundary extends Component<
    { children: ReactNode; onError: () => void },
    { hasError: boolean }
> {
    state = { hasError: false };

    static getDerivedStateFromError() {
        return { hasError: true };
    }

    componentDidCatch() {
        this.props.onError();
    }

    render() {
        return this.state.hasError ? null : this.props.children;
    }
}

/** The SVG stays in place until a supported, visible WebGL scene is ready. */
export function DestinationParkingExperience({
    signal,
}: {
    signal: DestinationScrollController;
}) {
    const [isNear, setIsNear] = useState(false);
    const [isVisible, setIsVisible] = useState(false);
    const [isMobile, setIsMobile] = useState(false);
    const [canUseWebGL, setCanUseWebGL] = useState(false);
    const [isCanvasReady, setIsCanvasReady] = useState(false);
    const [hasFailed, setHasFailed] = useState(false);
    const [element, setElement] = useState<HTMLDivElement | null>(null);

    useEffect(() => {
        const motionQuery = window.matchMedia(
            "(prefers-reduced-motion: reduce)",
        );
        const mobileQuery = window.matchMedia("(max-width: 767px)");
        const updateCapabilities = () => {
            setIsMobile(mobileQuery.matches);
            setCanUseWebGL(!motionQuery.matches && supportsWebGL());
            if (motionQuery.matches) setIsCanvasReady(false);
        };

        updateCapabilities();
        motionQuery.addEventListener("change", updateCapabilities);
        mobileQuery.addEventListener("change", updateCapabilities);
        return () => {
            motionQuery.removeEventListener("change", updateCapabilities);
            mobileQuery.removeEventListener("change", updateCapabilities);
        };
    }, []);

    useEffect(() => {
        if (!element || !window.IntersectionObserver) return;

        const prefetch = new IntersectionObserver(
            ([entry]) => {
                if (entry.isIntersecting) {
                    setIsNear(true);
                    prefetch.disconnect();
                }
            },
            { rootMargin: "250px 0px" },
        );
        const visibility = new IntersectionObserver(
            ([entry]) => setIsVisible(entry.isIntersecting),
            { threshold: 0.01 },
        );

        prefetch.observe(element);
        visibility.observe(element);
        return () => {
            prefetch.disconnect();
            visibility.disconnect();
        };
    }, [element]);

    const handleReady = useCallback(() => setIsCanvasReady(true), []);
    const handleFailure = useCallback(() => {
        setHasFailed(true);
        setIsCanvasReady(false);
    }, []);
    const shouldRenderScene = isNear && canUseWebGL && !hasFailed;

    return (
        <div
            ref={setElement}
            className={`parkwise-destination-scene ${isCanvasReady ? "has-canvas" : ""}`}
            role="img"
            aria-label="Illustrative parking lot with empty and occupied spaces; availability is not live"
        >
            <div className="parkwise-destination-fallback" aria-hidden="true" />
            {shouldRenderScene && (
                <SceneErrorBoundary onError={handleFailure}>
                    <div
                        className={`parkwise-destination-canvas ${isCanvasReady ? "is-ready" : ""}`}
                        aria-hidden="true"
                    >
                        <DestinationParkingScene
                            key={isMobile ? "mobile" : "desktop"}
                            signal={signal}
                            isMobile={isMobile}
                            isVisible={isVisible}
                            onReady={handleReady}
                            onFailure={handleFailure}
                        />
                    </div>
                </SceneErrorBoundary>
            )}
        </div>
    );
}
