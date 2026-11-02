"use client";

import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, useSyncExternalStore } from "react";
import {
    ArrowLeft,
    ArrowRight,
    CalendarDays,
    Camera,
    BatteryCharging,
    Bike,
    Building2,
    Accessibility,
    CarFront,
    Check,
    ChevronLeft,
    ChevronRight,
    Clock3,
    CreditCard,
    Images,
    Landmark,
    Layers3,
    Lightbulb,
    MapPin,
    Navigation,
    ShieldCheck,
    Star,
    Tag,
    Truck,
    Users,
    Wallet,
    Warehouse,
    Zap,
} from "lucide-react";
import { CustomerFooter, CustomerHeader } from "./CustomerShell";
import { ParkingLayoutView } from "./ParkingLayoutView";
import { ParkingLotBookingDialog } from "./booking/components/ParkingLotBookingDialog";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { getMyVehicles, type RegisteredVehicle } from "./myVehiclesApi";
import { isFutureBookingStart } from "./bookingDateTime";
import type { DailyOpeningHours } from "./parkingLotApi";

export type ParkingLotDetailView = {
    id: string;
    name: string;
    address: string;
    type: string;
    image: string;
    photos: string[];
    isPreview: boolean;
    latitude?: number;
    longitude?: number;
    openingHours?: DailyOpeningHours[];
    hourlyPrice?: number;
    distanceKm?: number;
    travelMinutes?: number;
    rating?: number;
    reviewCount?: number;
    spaces?: number;
    /** Prices and free spaces from Parking (lot detail), already worded for the page. */
    priceText?: string | null;
    spacesText?: string | null;
    compatible?: string[];
    amenities?: string[];
};

const vehicleLabels: Record<string, string> = {
    car: "Car",
    motorbike: "Motorbike",
    ev: "Electric vehicle (EV)",
    large: "Large vehicle",
};

const amenityLabels: Record<string, string> = {
    covered: "Covered parking",
    "always-open": "24/7 access",
    cctv: "CCTV security",
    "ev-charging": "EV charging",
    restrooms: "Restrooms",
    lighting: "Lighting",
    accessible: "Disabled access",
    "security-staff": "Security staff",
};

function vehicleIcon(kind: string) {
    switch (kind) {
        case "motorbike":
            return <Bike size={17} />;
        case "ev":
            return <BatteryCharging size={17} />;
        case "large":
            return <Truck size={17} />;
        default:
            return <CarFront size={17} />;
    }
}

function amenityIcon(kind: string) {
    switch (kind) {
        case "ev-charging":
            return <Zap size={14} />;
        case "restrooms":
            return <Users size={14} />;
        case "covered":
            return <Warehouse size={14} />;
        case "lighting":
            return <Lightbulb size={14} />;
        case "always-open":
            return <Clock3 size={14} />;
        case "accessible":
            return <Accessibility size={14} />;
        case "cctv":
            return <Camera size={14} />;
        default:
            return <ShieldCheck size={14} />;
    }
}

const weekdayNames = [
    "Sunday",
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
];

function formatMoney(amount: number) {
    return `${amount.toLocaleString("vi-VN")} ₫`;
}

function todayDate() {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}

function initialBookingDate() {
    const start = new Date();
    if (start.getHours() >= 10) start.setDate(start.getDate() + 1);
    return `${start.getFullYear()}-${String(start.getMonth() + 1).padStart(2, "0")}-${String(start.getDate()).padStart(2, "0")}`;
}

function hoursLabel(day: DailyOpeningHours) {
    if (day.isClosed) return "Closed";
    if (day.is24Hours) return "Open 24 hours";
    return `${day.opensAt} – ${day.closesAt}${day.closesAt! < day.opensAt! ? " (next day)" : ""}`;
}

export function ParkingLotDetailExperience({
    lot,
    fontClassName,
}: {
    lot: ParkingLotDetailView;
    fontClassName: string;
}) {
    const router = useRouter();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const [photoIndex, setPhotoIndex] = useState(0);
    const [selectedSpot, setSelectedSpot] = useState<string | null>(null);
    const [vehicles, setVehicles] = useState<RegisteredVehicle[]>([]);
    const [vehiclesState, setVehiclesState] = useState<
        "loading" | "ready" | "error"
    >("loading");
    const [vehicleId, setVehicleId] = useState("");
    const [bookingDialogOpen, setBookingDialogOpen] = useState(false);
    const [bookingMessage, setBookingMessage] = useState("");
    const [date, setDate] = useState(initialBookingDate);
    const [time, setTime] = useState("10:00");
    const [duration, setDuration] = useState(2);
    const photos = lot.photos.length ? lot.photos : [lot.image];
    const directionsHref =
        lot.latitude !== undefined && lot.longitude !== undefined
            ? `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(`${lot.latitude},${lot.longitude}`)}`
            : null;
    const selectedVehicle = vehicles.find(
        (item) => item.vehicleId === vehicleId,
    );
    const compatibleVehicles = vehicles.filter(
        (item) =>
            !lot.compatible?.length ||
            lot.compatible.includes(item.vehicleType.toLowerCase()) ||
            (item.vehicleType === "Car" &&
                lot.compatible.some(
                    (kind) => kind === "ev" || kind === "large",
                )),
    );

    useEffect(() => {
        if (!session) return;
        const controller = new AbortController();
        getMyVehicles(controller.signal)
            .then((result) => {
                setVehicles(result.vehicles);
                const eligible = result.vehicles.find(
                    (item) =>
                        !lot.compatible?.length ||
                        lot.compatible.includes(
                            item.vehicleType.toLowerCase(),
                        ) ||
                        (item.vehicleType === "Car" &&
                            lot.compatible.some(
                                (kind) => kind === "ev" || kind === "large",
                            )),
                );
                setVehicleId(eligible?.vehicleId ?? "");
                setVehiclesState("ready");
            })
            .catch(() => {
                if (!controller.signal.aborted) setVehiclesState("error");
            });
        return () => controller.abort();
    }, [session, lot.id, lot.compatible]);

    function startBooking() {
        setBookingMessage("");
        if (!session) {
            router.push("/login");
            return;
        }
        if (vehiclesState === "loading") {
            setBookingMessage(
                "Loading your vehicles. Please try again in a moment.",
            );
            return;
        }
        if (vehiclesState === "error") {
            setBookingMessage(
                "Could not load your vehicles. Please try again.",
            );
            return;
        }
        if (
            !selectedVehicle ||
            !compatibleVehicles.some(
                (item) => item.vehicleId === selectedVehicle.vehicleId,
            )
        ) {
            router.push("/customer/vehicles");
            return;
        }
        if (!isFutureBookingStart(date, time)) {
            setBookingMessage("Choose a future date and time.");
            return;
        }
        setBookingDialogOpen(true);
    }

    return (
        <div
            lang="en"
            className={`parkwise-page customer-shell-page lot-detail-page ${fontClassName}`}
        >
            <CustomerHeader active="detail" />
            <main className="parkwise-container lot-detail-main">
                <div className="lot-detail-grid">
                    <div className="lot-detail-left">
                        <header className="lot-detail-header">
                            <div className="lot-detail-title-row">
                                <h1>{lot.name}</h1>
                                {lot.isPreview && (
                                    <span className="lot-detail-status">
                                        <span aria-hidden="true" />
                                        Open now
                                    </span>
                                )}
                            </div>
                            <div className="lot-detail-address">
                                <MapPin size={17} aria-hidden="true" />
                                <span>{lot.address}</span>
                                {directionsHref && (
                                    <a
                                        href={directionsHref}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        View on map <ArrowRight size={15} />
                                    </a>
                                )}
                            </div>
                            <div className="lot-detail-vehicles">
                                {lot.rating !== undefined && (
                                    <span className="lot-detail-rating">
                                        <Star size={17} fill="currentColor" />
                                        <strong>{lot.rating.toFixed(1)}</strong>
                                        <small>
                                            ({lot.reviewCount} reviews)
                                        </small>
                                    </span>
                                )}
                                {!lot.isPreview && (
                                    <span className="lot-detail-lot-type">
                                        <Building2 size={17} /> {lot.type}
                                    </span>
                                )}
                                {lot.compatible?.map((item) => (
                                    <span key={item}>
                                        {vehicleIcon(item)}{" "}
                                        {vehicleLabels[item] ?? item}
                                    </span>
                                ))}
                            </div>
                        </header>

                        <div
                            className="lot-detail-metrics"
                            aria-label="Parking summary"
                        >
                            <div>
                                <Navigation size={21} />
                                <span>
                                    <strong>
                                        {lot.distanceKm === undefined
                                            ? "—"
                                            : `${lot.distanceKm} km`}
                                    </strong>
                                    <small>
                                        {lot.isPreview
                                            ? "from your location"
                                            : "distance unavailable"}
                                    </small>
                                </span>
                            </div>
                            <div>
                                <Clock3 size={21} />
                                <span>
                                    <strong>
                                        {lot.travelMinutes === undefined
                                            ? "—"
                                            : `${lot.travelMinutes} mins`}
                                    </strong>
                                    <small>
                                        {lot.travelMinutes === undefined
                                            ? "travel time unavailable"
                                            : "estimated travel time"}
                                    </small>
                                </span>
                            </div>
                            <div>
                                <Tag size={21} />
                                <span>
                                    <strong>
                                        {lot.priceText ??
                                            (lot.hourlyPrice === undefined
                                                ? "—"
                                                : formatMoney(lot.hourlyPrice))}
                                    </strong>
                                    <small>
                                        {lot.priceText
                                            ? "current price"
                                            : "no price set yet"}
                                    </small>
                                </span>
                            </div>
                            <div>
                                <CarFront size={21} />
                                <span>
                                    <strong>
                                        {lot.spacesText ??
                                            (lot.spaces === undefined
                                                ? "—"
                                                : lot.spaces)}
                                    </strong>
                                    <small>
                                        {lot.spacesText
                                            ? "free spaces now"
                                            : "availability unavailable"}
                                    </small>
                                </span>
                            </div>
                        </div>

                        <div className="lot-detail-feature-grid">
                            <div className="lot-detail-media-column">
                                <div className="lot-detail-gallery">
                                    <Image
                                        key={photos[photoIndex]}
                                        src={photos[photoIndex]}
                                        alt={`Parking view ${photoIndex + 1}`}
                                        fill
                                        sizes="(max-width: 900px) 100vw, 600px"
                                        priority
                                    />
                                    <span className="lot-detail-gallery-count">
                                        <Images size={14} aria-hidden="true" />{" "}
                                        View all photos ({photos.length})
                                    </span>
                                    {photos.length > 1 && (
                                        <>
                                            <button
                                                type="button"
                                                className="lot-detail-gallery-prev"
                                                aria-label="Previous photo"
                                                onClick={() =>
                                                    setPhotoIndex(
                                                        (index) =>
                                                            (index +
                                                                photos.length -
                                                                1) %
                                                            photos.length,
                                                    )
                                                }
                                            >
                                                <ChevronLeft size={23} />
                                            </button>
                                            <button
                                                type="button"
                                                className="lot-detail-gallery-next"
                                                aria-label="Next photo"
                                                onClick={() =>
                                                    setPhotoIndex(
                                                        (index) =>
                                                            (index + 1) %
                                                            photos.length,
                                                    )
                                                }
                                            >
                                                <ChevronRight size={23} />
                                            </button>
                                        </>
                                    )}
                                </div>
                            </div>

                            <section
                                className="lot-detail-info-card"
                                aria-label="About this parking lot"
                            >
                                <h2>About this parking lot</h2>
                                <p>
                                    {lot.isPreview
                                        ? `${lot.name} is a modern parking facility in the heart of District 1, Ho Chi Minh City. Conveniently situated near Nguyen Hue Walking Street, shopping centers, and business districts.`
                                        : `${lot.name} is an approved ${lot.type.toLowerCase()} at ${lot.address}. Additional facility details have not been published yet.`}
                                </p>
                                <div className="lot-detail-info-split">
                                    <div>
                                        <h3>
                                            <Clock3 size={18} /> Opening hours
                                        </h3>
                                        {lot.openingHours?.length ? (
                                            <details className="lot-detail-hours-details">
                                                <summary>
                                                    View weekly schedule
                                                </summary>
                                                {lot.openingHours.map((day) => (
                                                    <div key={day.dayOfWeek}>
                                                        <span>
                                                            {
                                                                weekdayNames[
                                                                    day
                                                                        .dayOfWeek
                                                                ]
                                                            }
                                                        </span>
                                                        <strong>
                                                            {hoursLabel(day)}
                                                        </strong>
                                                    </div>
                                                ))}
                                                <small>
                                                    Vietnam time (UTC+7)
                                                </small>
                                            </details>
                                        ) : (
                                            <p>
                                                {lot.isPreview &&
                                                lot.amenities?.includes(
                                                    "always-open",
                                                )
                                                    ? "24/7 access"
                                                    : "Hours not published"}
                                            </p>
                                        )}
                                    </div>
                                    <div>
                                        <h3>
                                            <Layers3 size={18} /> Amenities
                                        </h3>
                                        {lot.amenities?.length ? (
                                            <ul className="lot-detail-amenities">
                                                {lot.amenities.map((item) => (
                                                    <li key={item}>
                                                        {amenityIcon(item)}
                                                        {amenityLabels[item] ??
                                                            item}
                                                    </li>
                                                ))}
                                            </ul>
                                        ) : (
                                            <p>Amenities not published</p>
                                        )}
                                    </div>
                                </div>
                                <div className="lot-detail-info-split lot-detail-info-bottom">
                                    <div>
                                        <h3>
                                            <CreditCard size={18} /> Payment
                                            methods
                                        </h3>
                                        {lot.isPreview ? (
                                            <div
                                                className="lot-detail-payment-icons"
                                                aria-label="Payment methods"
                                            >
                                                <span title="Card example">
                                                    <CreditCard size={18} />
                                                </span>
                                                <span title="Wallet example">
                                                    <Wallet size={18} />
                                                </span>
                                                <span title="Bank example">
                                                    <Landmark size={18} />
                                                </span>
                                            </div>
                                        ) : (
                                            <p>Not published</p>
                                        )}
                                    </div>
                                    <div>
                                        <h3>
                                            <ShieldCheck size={18} /> Parking
                                            rules
                                        </h3>
                                        {lot.isPreview ? (
                                            <ul className="lot-detail-rules">
                                                <li>
                                                    <Check size={14} /> Follow
                                                    staff instructions
                                                </li>
                                                <li>
                                                    <Check size={14} /> Park
                                                    within marked spaces
                                                </li>
                                                <li>
                                                    <Check size={14} /> Keep
                                                    valuables secure
                                                </li>
                                            </ul>
                                        ) : (
                                            <p>
                                                Ask the parking operator for
                                                current rules.
                                            </p>
                                        )}
                                    </div>
                                </div>
                            </section>
                        </div>

                        {lot.isPreview && (
                            <ParkingLayoutView
                                selectedSpot={selectedSpot}
                                onSelectSpot={setSelectedSpot}
                            />
                        )}
                    </div>

                    <aside
                        className="lot-detail-booking-card"
                        aria-labelledby="lot-detail-booking-title"
                    >
                        <div className="lot-detail-booking-heading">
                            <h2 id="lot-detail-booking-title">
                                Book this parking
                            </h2>
                        </div>
                        <div className="lot-detail-booking-fields">
                            <label>
                                <span>
                                    <CarFront size={21} />
                                </span>
                                <small>Your vehicle</small>
                                <select
                                    value={vehicleId}
                                    onChange={(event) =>
                                        setVehicleId(event.target.value)
                                    }
                                    disabled={
                                        !session ||
                                        vehiclesState !== "ready" ||
                                        compatibleVehicles.length === 0
                                    }
                                >
                                    {compatibleVehicles.length === 0 && (
                                        <option value="">
                                            {!session
                                                ? "Sign in to choose a vehicle"
                                                : vehiclesState === "loading"
                                                  ? "Loading vehicles"
                                                  : "Add a vehicle to continue"}
                                        </option>
                                    )}
                                    {compatibleVehicles.map((item) => (
                                        <option
                                            key={item.vehicleId}
                                            value={item.vehicleId}
                                        >
                                            {item.nickName
                                                ? `${item.nickName} · `
                                                : ""}
                                            {item.licensePlate} ·{" "}
                                            {item.vehicleType}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            <div className="lot-detail-vehicle-helper">
                                <Link
                                    href={
                                        session
                                            ? "/customer/vehicles"
                                            : "/login"
                                    }
                                >
                                    {session
                                        ? "Manage my vehicles"
                                        : "Sign in to use My Vehicles"}
                                </Link>
                            </div>
                            <label>
                                <span>
                                    <CalendarDays size={21} />
                                </span>
                                <small>Date</small>
                                <input
                                    type="date"
                                    min={todayDate()}
                                    value={date}
                                    onChange={(event) =>
                                        setDate(event.target.value)
                                    }
                                />
                            </label>
                            <label>
                                <span>
                                    <Clock3 size={21} />
                                </span>
                                <small>Time</small>
                                <input
                                    type="time"
                                    value={time}
                                    onChange={(event) =>
                                        setTime(event.target.value)
                                    }
                                />
                            </label>
                            <label>
                                <span>
                                    <Clock3 size={21} />
                                </span>
                                <small>Duration</small>
                                <select
                                    value={duration}
                                    onChange={(event) =>
                                        setDuration(Number(event.target.value))
                                    }
                                >
                                    {[1, 2, 3, 4, 6, 8].map((hours) => (
                                        <option key={hours} value={hours}>
                                            {hours}{" "}
                                            {hours === 1 ? "hour" : "hours"}
                                        </option>
                                    ))}
                                </select>
                            </label>
                        </div>
                        <div className="lot-detail-booking-total">
                            <div>
                                <span>Hourly rate</span>
                                <strong>
                                    {lot.hourlyPrice === undefined
                                        ? "Unavailable"
                                        : formatMoney(lot.hourlyPrice)}
                                </strong>
                            </div>
                            <div>
                                <span>Duration</span>
                                <strong>
                                    {duration}{" "}
                                    {duration === 1 ? "hour" : "hours"}
                                </strong>
                            </div>
                            <div>
                                <span>Estimated total</span>
                                <strong>
                                    {lot.hourlyPrice === undefined
                                        ? "Unavailable"
                                        : formatMoney(
                                              lot.hourlyPrice * duration,
                                          )}
                                </strong>
                            </div>
                        </div>
                        {lot.isPreview && (
                            <div className="lot-detail-booking-notice">
                                <CarFront size={20} aria-hidden="true" />
                                <span>
                                    <strong>
                                        {lot.spaces} spaces available
                                    </strong>
                                </span>
                            </div>
                        )}
                        {bookingMessage && (
                            <p
                                className="lot-detail-booking-error"
                                role="alert"
                            >
                                {bookingMessage}
                            </p>
                        )}
                        <button
                            type="button"
                            className="lot-detail-book-now"
                            onClick={startBooking}
                        >
                            Book Now <ArrowRight size={18} />
                        </button>
                    </aside>
                </div>

                <Link href="/search" className="lot-detail-bottom-back">
                    <ArrowLeft size={16} /> Back to Find Parking
                </Link>
            </main>
            <CustomerFooter />
            {bookingDialogOpen && selectedVehicle && (
                <ParkingLotBookingDialog
                    isOpen={bookingDialogOpen}
                    onClose={() => setBookingDialogOpen(false)}
                    lot={lot}
                    vehicle={selectedVehicle}
                    date={date}
                    time={time}
                    duration={duration}
                    selectedSpot={selectedSpot}
                />
            )}
        </div>
    );
}
