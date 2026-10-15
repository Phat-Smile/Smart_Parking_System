import { useEffect, type RefObject } from "react";

export class DestinationScrollController {
    private progress = 0;
    private listeners = new Set<(progress: number) => void>();

    setProgress(progress: number) {
        this.progress = progress;
        this.listeners.forEach((listener) => listener(progress));
    }

    subscribe(listener: (progress: number) => void) {
        this.listeners.add(listener);
        listener(this.progress);
        return () => {
            this.listeners.delete(listener);
        };
    }
}

/** Motion is progressive enhancement: the server-rendered page stays visible. */
export function useParkWiseLandingMotion(
    pageRef: RefObject<HTMLDivElement | null>,
    destinationScroll: DestinationScrollController,
) {
    useEffect(() => {
        const page = pageRef.current;
        if (!page) return;

        let isDisposed = false;
        let revertMotion: (() => void) | undefined;

        void Promise.all([import("gsap"), import("gsap/ScrollTrigger")])
            .then(([{ gsap }, { ScrollTrigger }]) => {
                if (isDisposed) return;

                gsap.registerPlugin(ScrollTrigger);
                const media = gsap.matchMedia();
                revertMotion = () => media.revert();

                media.add("(prefers-reduced-motion: no-preference)", () => {
                    const context = gsap.context(() => {
                        const one = (selector: string) =>
                            page.querySelector<HTMLElement>(selector);
                        const all = (selector: string) =>
                            Array.from(
                                page.querySelectorAll<HTMLElement>(selector),
                            );

                        const hero = one(".parkwise-hero");
                        const heroImage = one(".parkwise-hero-image");
                        const searchCard = one(".parkwise-search-card");
                        if (hero) {
                            gsap.timeline({ defaults: { ease: "power2.out" } })
                                .from(all(".parkwise-hero-copy > *"), {
                                    autoAlpha: 0,
                                    y: 20,
                                    duration: 0.8,
                                    stagger: 0.12,
                                    clearProps: "all",
                                })
                                .from(
                                    searchCard,
                                    {
                                        autoAlpha: 0,
                                        y: 18,
                                        scale: 0.985,
                                        duration: 0.85,
                                        clearProps: "all",
                                    },
                                    "-=0.48",
                                );

                            if (searchCard) {
                                gsap.to(searchCard, {
                                    y: -7,
                                    duration: 2.8,
                                    delay: 1.4,
                                    ease: "sine.inOut",
                                    repeat: -1,
                                    yoyo: true,
                                    scrollTrigger: {
                                        trigger: hero,
                                        start: "top bottom",
                                        end: "bottom top",
                                        toggleActions:
                                            "play pause resume pause",
                                    },
                                });
                            }

                            if (
                                heroImage &&
                                window.matchMedia("(min-width: 768px)").matches
                            ) {
                                gsap.fromTo(
                                    heroImage,
                                    { yPercent: -2, scale: 1.06 },
                                    {
                                        yPercent: 2,
                                        scale: 1.06,
                                        ease: "none",
                                        scrollTrigger: {
                                            trigger: hero,
                                            start: "top top",
                                            end: "bottom top",
                                            scrub: 0.8,
                                        },
                                    },
                                );
                            }
                        }

                        const how = one(".parkwise-how");
                        if (how) {
                            gsap.from(all(".parkwise-step"), {
                                autoAlpha: 0,
                                x: -12,
                                y: 18,
                                rotationY: -5,
                                transformPerspective: 800,
                                duration: 0.65,
                                stagger: 0.13,
                                ease: "power2.out",
                                clearProps: "all",
                                scrollTrigger: {
                                    trigger: how,
                                    start: "top 78%",
                                    once: true,
                                },
                            });
                        }

                        const nearby = one(".parkwise-nearby");
                        if (nearby) {
                            const reveal = gsap
                                .timeline({ paused: true })
                                .from(
                                    one(
                                        ".parkwise-nearby .parkwise-section-title",
                                    ),
                                    {
                                        autoAlpha: 0,
                                        scale: 0.96,
                                        duration: 0.75,
                                        ease: "power2.out",
                                        clearProps: "all",
                                    },
                                )
                                .from(
                                    one(".parkwise-nearby-message > p"),
                                    {
                                        autoAlpha: 0,
                                        y: 12,
                                        duration: 0.55,
                                        ease: "power2.out",
                                        clearProps: "all",
                                    },
                                    "-=0.24",
                                )
                                .from(
                                    one(".parkwise-nearby-login"),
                                    {
                                        autoAlpha: 0,
                                        duration: 0.5,
                                        clearProps: "opacity,visibility",
                                    },
                                    "-=0.2",
                                );

                            const cameraProgress = { value: 0 };
                            gsap.to(cameraProgress, {
                                value: 1,
                                ease: "none",
                                onUpdate: () =>
                                    destinationScroll.setProgress(
                                        cameraProgress.value,
                                    ),
                                scrollTrigger: {
                                    trigger: nearby,
                                    start: "top bottom",
                                    end: "bottom top",
                                    scrub: 0.75,
                                    onUpdate: (self) => {
                                        if (self.progress > 0.16) reveal.play();
                                    },
                                    onRefresh: (self) => {
                                        if (self.progress > 0.16) {
                                            reveal.progress(1);
                                        }
                                    },
                                },
                            });
                        }

                        const future = one(".parkwise-future");
                        if (future) {
                            const reveal = gsap
                                .timeline({ paused: true })
                                .from(
                                    all(
                                        ".parkwise-future > .parkwise-container > p, .parkwise-future h2",
                                    ),
                                    {
                                        autoAlpha: 0,
                                        y: 14,
                                        duration: 0.6,
                                        stagger: 0.1,
                                        ease: "power2.out",
                                        clearProps: "all",
                                    },
                                )
                                .from(
                                    all(".parkwise-stats > div"),
                                    {
                                        autoAlpha: 0,
                                        y: 20,
                                        rotationX: -9,
                                        rotationY: -5,
                                        transformPerspective: 900,
                                        transformOrigin: "center bottom",
                                        duration: 0.72,
                                        stagger: 0.12,
                                        ease: "power2.out",
                                        clearProps: "all",
                                    },
                                    "-=0.2",
                                );

                            const stats = one(".parkwise-stats");
                            if (stats) {
                                gsap.fromTo(
                                    stats,
                                    { rotationX: 1.5, rotationY: -2 },
                                    {
                                        rotationX: -1.5,
                                        rotationY: 2,
                                        ease: "none",
                                        scrollTrigger: {
                                            trigger: future,
                                            start: "top bottom",
                                            end: "bottom top",
                                            scrub: 0.9,
                                            onUpdate: (self) => {
                                                if (self.progress > 0.16) {
                                                    reveal.play();
                                                }
                                            },
                                            onRefresh: (self) => {
                                                if (self.progress > 0.16) {
                                                    reveal.progress(1);
                                                }
                                            },
                                        },
                                    },
                                );
                            }
                        }

                        const join = one(".parkwise-newsletter");
                        if (join) {
                            gsap.timeline({
                                scrollTrigger: {
                                    trigger: join,
                                    start: "top 82%",
                                    once: true,
                                },
                            }).from(
                                all(
                                    ".parkwise-newsletter > .parkwise-container > p, .parkwise-newsletter h2, .parkwise-newsletter-input",
                                ),
                                {
                                    autoAlpha: 0,
                                    y: 14,
                                    duration: 0.7,
                                    stagger: 0.14,
                                    ease: "power2.out",
                                    clearProps: "all",
                                },
                            );

                            const pattern = one(".parkwise-newsletter-pattern");
                            if (pattern) {
                                gsap.fromTo(
                                    pattern,
                                    { yPercent: -2, scale: 1.04 },
                                    {
                                        yPercent: 2,
                                        scale: 1.04,
                                        ease: "none",
                                        scrollTrigger: {
                                            trigger: join,
                                            start: "top bottom",
                                            end: "bottom top",
                                            scrub: 1,
                                        },
                                    },
                                );
                            }
                        }

                        const footer = one(".parkwise-footer");
                        if (footer) {
                            gsap.from(
                                all(
                                    ".parkwise-footer-grid > *, .parkwise-footer-bottom",
                                ),
                                {
                                    y: 14,
                                    duration: 0.62,
                                    stagger: 0.09,
                                    ease: "power2.out",
                                    clearProps: "all",
                                    scrollTrigger: {
                                        trigger: footer,
                                        start: "top 85%",
                                        once: true,
                                    },
                                },
                            );
                        }
                    }, page);

                    return () => context.revert();
                });
            })
            .catch(() => {
                // A failed optional animation bundle must not hide page content.
                revertMotion?.();
            });

        return () => {
            isDisposed = true;
            revertMotion?.();
        };
    }, [pageRef, destinationScroll]);
}
