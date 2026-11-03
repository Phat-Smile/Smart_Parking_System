"use client";

import { useMemo, useRef, useState, type FormEvent } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { ParkWiseFooter } from "./ParkWiseFooter";
import { ParkWiseGridInfo } from "./ParkWiseGridInfo";
import { ParkWiseHowItWorks } from "./ParkWiseHowItWorks";
import { ParkWiseNavbar } from "./ParkWiseNavbar";
import { DestinationParkingExperience } from "./DestinationParkingExperience";
import {
    useParkWiseLandingMotion,
    DestinationScrollController,
} from "./useParkWiseLandingMotion";

/**
 * Port of ParkWise's src/Pages/Homepage.jsx and its DefaultLayout. The original
 * frontend's navigation, section order, copy, imagery, and component markup are
 * retained. Guest search is a preview that leads to sign-in.
 */
export function ParkWiseLanding({ fontClassName }: { fontClassName: string }) {
    const router = useRouter();
    const [address, setAddress] = useState("");
    const [locationBusy, setLocationBusy] = useState(false);
    const [locationMessage, setLocationMessage] = useState<string | null>(null);
    const pageRef = useRef<HTMLDivElement>(null);
    const destinationScroll = useMemo(
        () => new DestinationScrollController(),
        [],
    );
    useParkWiseLandingMotion(pageRef, destinationScroll);

    function useCurrentLocation() {
        if (typeof window === "undefined" || !navigator.geolocation) {
            setLocationMessage(
                "Location is not supported by your browser. Please enter an address.",
            );
            return;
        }
        setLocationBusy(true);
        setLocationMessage("Detecting current location…");
        navigator.geolocation.getCurrentPosition(
            ({ coords }) => {
                setAddress("Current location");
                setLocationBusy(false);
                setLocationMessage(
                    `Current location selected (${coords.latitude.toFixed(4)}, ${coords.longitude.toFixed(4)}).`,
                );
            },
            (error) => {
                setLocationBusy(false);
                setLocationMessage(
                    error.code === 1
                        ? "Location permission was denied. Please enter an address."
                        : "Unable to retrieve your location. Please enter an address.",
                );
            },
            { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 },
        );
    }

    function handleSearch(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        router.push("/login");
    }

    return (
        <div
            ref={pageRef}
            className={`parkwise-page parkwise-landing ${fontClassName}`}
        >
            <ParkWiseNavbar />
            <main>
                {/* ParkWise Homepage.jsx: hero and search */}
                <section className="parkwise-hero" id="search">
                    <Image
                        src="/images/parkwise/hero-parking-garage.png"
                        alt=""
                        fill
                        priority
                        unoptimized={process.env.NODE_ENV === "development"}
                        sizes="(max-width: 767px) 1px, 100vw"
                        className="parkwise-hero-image"
                    />
                    <div className="parkwise-container parkwise-hero-content">
                        <div className="parkwise-hero-copy">
                            <p className="parkwise-eyebrow">SMART PARKING</p>
                            <h1>Find the Right Parking Spot, Smarter</h1>
                            <p>
                                Find nearby parking lots based on your
                                destination, vehicle type, availability,
                                distance, and estimated travel time. Sign in for
                                recommendations tailored to your journey.
                            </p>
                            <p className="parkwise-hero-extra">
                                Explore parking layouts in 2D/3D before driving
                                directly to a parking lot. Sign in to access
                                account features.
                            </p>
                        </div>
                        <div className="parkwise-search-card">
                            <div className="parkwise-search-heading">
                                <span className="parkwise-search-eyebrow">
                                    FIND A SPOT
                                </span>
                                <h2>Where do you want to park?</h2>
                                <p>
                                    Enter a destination to start your parking
                                    search.
                                </p>
                            </div>
                            <form onSubmit={handleSearch}>
                                <div className="parkwise-search-field">
                                    <Image
                                        src="/images/parkwise/icon.svg"
                                        alt=""
                                        width={22}
                                        height={22}
                                    />
                                    <input
                                        type="text"
                                        value={address}
                                        onChange={(event) => {
                                            setAddress(event.target.value);
                                            if (locationMessage)
                                                setLocationMessage(null);
                                        }}
                                        placeholder="Street, landmark, or district"
                                        aria-label="Destination"
                                        required
                                    />
                                    <button
                                        type="button"
                                        className={`parkwise-search-locate ${locationBusy ? "is-loading" : ""}`}
                                        onClick={useCurrentLocation}
                                        disabled={locationBusy}
                                        aria-label="Use my current location"
                                        title="Use my current location"
                                    >
                                        <svg
                                            width={19}
                                            height={19}
                                            viewBox="0 0 24 24"
                                            fill="none"
                                            stroke="currentColor"
                                            strokeWidth="2"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                            aria-hidden="true"
                                        >
                                            <circle cx="12" cy="12" r="7" />
                                            <line
                                                x1="12"
                                                y1="2"
                                                x2="12"
                                                y2="5"
                                            />
                                            <line
                                                x1="12"
                                                y1="19"
                                                x2="12"
                                                y2="22"
                                            />
                                            <line
                                                x1="2"
                                                y1="12"
                                                x2="5"
                                                y2="12"
                                            />
                                            <line
                                                x1="19"
                                                y1="12"
                                                x2="22"
                                                y2="12"
                                            />
                                            <circle
                                                cx="12"
                                                cy="12"
                                                r="2"
                                                fill="currentColor"
                                            />
                                        </svg>
                                    </button>
                                </div>
                                <button
                                    className="parkwise-button parkwise-search-button"
                                    type="submit"
                                >
                                    Search
                                </button>
                            </form>
                            {locationMessage ? (
                                <p
                                    className="parkwise-search-hint parkwise-search-location-hint"
                                    role="status"
                                >
                                    {locationMessage}
                                </p>
                            ) : (
                                <p className="parkwise-search-hint">
                                    Sign in to see nearby parking and
                                    availability.
                                </p>
                            )}
                        </div>
                    </div>
                </section>

                <ParkWiseHowItWorks />

                <section id="nearby-parking" className="parkwise-nearby">
                    <DestinationParkingExperience signal={destinationScroll} />
                    <div className="parkwise-container">
                        <div className="parkwise-section-title">
                            <p>Find parking near</p>
                            <h2>your destination</h2>
                        </div>
                    </div>
                </section>
                {/* ParkWise Homepage.jsx: GridInfo */}
                <section id="about" className="parkwise-future">
                    <div className="parkwise-container">
                        <p>SMART PARKING, HASSLE-FREE</p>
                        <h2>
                            The Future of Parking{" "}
                            <br className="parkwise-desktop-break" /> Is Here
                        </h2>
                        <ParkWiseGridInfo />
                    </div>
                </section>

                <section id="newsletter" className="parkwise-newsletter">
                    <Image
                        src="/images/parkwise/patern.svg"
                        alt=""
                        fill
                        sizes="100vw"
                        loading="eager"
                        className="parkwise-newsletter-pattern"
                    />
                    <div className="parkwise-container">
                        <h2>
                            Ready for a smarter <br /> parking experience?
                        </h2>
                    </div>
                </section>
            </main>
            <ParkWiseFooter />
        </div>
    );
}
