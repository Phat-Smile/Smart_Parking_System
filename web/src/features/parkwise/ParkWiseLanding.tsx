"use client";

import { useMemo, useRef, useState, type FormEvent } from "react";
import Image from "next/image";
import Link from "next/link";
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
    const pageRef = useRef<HTMLDivElement>(null);
    const destinationScroll = useMemo(
        () => new DestinationScrollController(),
        [],
    );
    useParkWiseLandingMotion(pageRef, destinationScroll);

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
                            <div className="parkwise-search-tabs">
                                <span>Where do you want to park?</span>
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
                                        onChange={(event) =>
                                            setAddress(event.target.value)
                                        }
                                        placeholder="Enter your destination"
                                        aria-label="Destination"
                                    />
                                </div>
                                <button
                                    className="parkwise-button parkwise-search-button"
                                    type="submit"
                                >
                                    Search
                                </button>
                            </form>
                            <p className="parkwise-search-hint">
                                Sign in to search for parking.
                            </p>
                        </div>
                    </div>
                </section>

                <ParkWiseHowItWorks />

                {/* Guest preview only: parking search requires an account. */}
                <section id="nearby-parking" className="parkwise-nearby">
                    <DestinationParkingExperience signal={destinationScroll} />
                    <div className="parkwise-container">
                        <div className="parkwise-section-title">
                            <p>Find parking near</p>
                            <h2>your destination</h2>
                        </div>
                        <div className="parkwise-nearby-message">
                            <p>
                                Sign in to explore parking near your
                                destination.
                            </p>
                            <Link
                                href="/login"
                                className="parkwise-button parkwise-nearby-login"
                            >
                                Log in to search
                            </Link>
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

                {/* Account invitation: newsletter delivery is not implemented. */}
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
                        <p>JOIN PARKWISE</p>
                        <h2>
                            Ready for a smarter <br /> parking experience?
                        </h2>
                        <div className="parkwise-newsletter-input" style={{ display: 'flex', gap: '14px', flexWrap: 'wrap', justifyContent: 'center' }}>
                            <Link href="/sign-up" className="parkwise-button">
                                Đăng ký Lái xe
                            </Link>
                            <Link
                                href="/owner/register"
                                className="parkwise-button"
                                style={{
                                    backgroundColor: '#2563eb',
                                    borderColor: '#2563eb',
                                    color: '#ffffff',
                                }}
                            >
                                Register as Lot Owner
                            </Link>
                        </div>
                    </div>
                </section>
            </main>
            <ParkWiseFooter />
        </div>
    );
}
