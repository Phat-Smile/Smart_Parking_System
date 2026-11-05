"use client";

import {
    useCallback,
    useEffect,
    useState,
    useSyncExternalStore,
    type FormEvent,
} from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";

import { CustomerFooter, CustomerHeader, CustomerIcon } from "./CustomerShell";
import { ActiveSessionCard } from "./CustomerSessions";
import { CustomerSearchDropdown } from "./CustomerSearchDropdown";
import { dashboardPreviewData } from "./dashboardPreviewData";
import { useCustomerLanguage } from "./i18n/customerLanguage";

type SearchTab = "find" | "quick" | "saved";
const recentPlaces = [
    "Saigon Centre",
    "Ben Thanh Market",
    "Bitexco Tower",
    "Tan Son Nhat Airport",
];
const savedPlaces = [
    { name: "Work", detail: "Bitexco Tower", icon: "car" as const },
    { name: "Home", detail: "District 7", icon: "location" as const },
    { name: "Favorite", detail: "Saigon Centre", icon: "bookmark" as const },
];
const vehicleOptions = [
    { value: "any", label: "Any vehicle", icon: "car" as const },
    { value: "car", label: "Car", icon: "car" as const },
    { value: "motorbike", label: "Motorbike", icon: "motorbike" as const },
];

function subscribeClock(onChange: () => void) {
    const interval = window.setInterval(onChange, 30_000);
    return () => window.clearInterval(interval);
}

function getClockSnapshot() {
    return Math.floor(Date.now() / 60_000);
}

function getServerClockSnapshot() {
    return null;
}

function formatLocalDate(date: Date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

export function CustomerDashboardPage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const router = useRouter();
    const { copy, language } = useCustomerLanguage();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const currentMinute = useSyncExternalStore(
        subscribeClock,
        getClockSnapshot,
        getServerClockSnapshot,
    );
    const localNow =
        currentMinute === null ? null : new Date(currentMinute * 60_000);
    const hour = localNow?.getHours();
    const greeting =
        hour === undefined
            ? copy("Welcome,", "Chào mừng,")
            : hour < 12
              ? copy("Good morning,", "Chào buổi sáng,")
              : hour < 17
                ? copy("Good afternoon,", "Chào buổi chiều,")
                : copy("Good evening,", "Chào buổi tối,");
    const localTime = localNow?.toLocaleTimeString(language === "vi" ? "vi-VN" : "en-US", {
        hour: "numeric",
        minute: "2-digit",
    });
    const [tab, setTab] = useState<SearchTab>("find");
    const [destination, setDestination] = useState("");
    const [selectedDate, setSelectedDate] = useState<string | null>(null);
    const [selectedTime, setSelectedTime] = useState<string | null>(null);
    const date = selectedDate ?? (localNow ? formatLocalDate(localNow) : "");
    const time =
        selectedTime ??
        (localNow
            ? `${String(localNow.getHours()).padStart(2, "0")}:${String(localNow.getMinutes()).padStart(2, "0")}`
            : "");
    const [vehicle, setVehicle] = useState("any");
    const [searchError, setSearchError] = useState("");
    const [locationMessage, setLocationMessage] = useState("");
    const [locating, setLocating] = useState(false);
    const [temperature, setTemperature] = useState<number | null>(null);
    const [weatherState, setWeatherState] = useState<
        "idle" | "loading" | "ready" | "unavailable"
    >("idle");
    const displayName = session?.displayName?.trim();

    const locateCurrentPosition = useCallback(() => {
        if (!navigator.geolocation) {
            setLocationMessage(
                "Location is unavailable in this browser. Enter an address instead.",
            );
            setWeatherState("unavailable");
            return;
        }
        setLocating(true);
        setWeatherState("loading");
        setLocationMessage("Finding your current location…");
        navigator.geolocation.getCurrentPosition(
            async (position) => {
                setDestination("Current location");
                setSearchError("");
                setLocationMessage("");
                setLocating(false);
                try {
                    const params = new URLSearchParams({
                        latitude: String(position.coords.latitude),
                        longitude: String(position.coords.longitude),
                        current: "temperature_2m",
                        temperature_unit: "celsius",
                    });
                    const response = await fetch(
                        `https://api.open-meteo.com/v1/forecast?${params}`,
                    );
                    if (!response.ok) throw new Error("Weather unavailable");
                    const data: { current?: { temperature_2m?: number } } =
                        await response.json();
                    if (typeof data.current?.temperature_2m !== "number") {
                        throw new Error("Temperature unavailable");
                    }
                    setTemperature(Math.round(data.current.temperature_2m));
                    setWeatherState("ready");
                } catch {
                    setWeatherState("unavailable");
                }
            },
            (error) => {
                setLocationMessage(
                    error.code === 1
                        ? "Location access was declined. Enter an address instead."
                        : "Your location could not be found. Enter an address instead.",
                );
                setLocating(false);
                setWeatherState("unavailable");
            },
            { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 },
        );
    }, [
        setDestination,
        setLocating,
        setLocationMessage,
        setSearchError,
        setTemperature,
        setWeatherState,
    ]);

    useEffect(() => {
        const timer = window.setTimeout(locateCurrentPosition, 0);
        return () => window.clearTimeout(timer);
    }, [locateCurrentPosition]);

    function handleSearch(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        if (!destination.trim()) {
            setSearchError("Enter a destination to find parking.");
            return;
        }
        const params = new URLSearchParams({
            destination: destination.trim(),
            date,
            time,
        });
        if (vehicle !== "any") params.set("vehicle", vehicle);
        router.push(`/search?${params.toString()}`);
    }

    function choosePlace(place: string) {
        setDestination(place);
        setTab("find");
        setSearchError("");
    }

    function quickSearch(place: string) {
        const params = new URLSearchParams({
            destination: place,
            date: localNow ? formatLocalDate(localNow) : date,
            time: localNow
                ? `${String(localNow.getHours()).padStart(2, "0")}:${String(localNow.getMinutes()).padStart(2, "0")}`
                : time,
        });
        if (vehicle !== "any") params.set("vehicle", vehicle);
        router.push(`/search?${params.toString()}`);
    }

    return (
        <div
            lang={language}
            className={`parkwise-page customer-shell-page customer-dashboard-page ${fontClassName}`}
        >
            <CustomerHeader active="home" />
            <main>
                <section
                    className="dashboard-hero"
                    aria-labelledby="dashboard-title"
                >
                    <Image
                        src="/images/parkwise/dashboard-hero-parking.png"
                        alt=""
                        fill
                        priority
                        sizes="100vw"
                        className="dashboard-hero-image"
                    />
                    <div className="dashboard-hero-shade" />
                    <div className="dashboard-container dashboard-hero-copy">
                        <p>{greeting}</p>
                        <h1 id="dashboard-title">
                            {displayName || copy("Find your space", "Tìm chỗ đỗ của bạn")}{" "}
                            <span aria-hidden="true">👋</span>
                        </h1>
                        <h2>{copy("Smarter parking. Brighter journeys.", "Đỗ xe thông minh. Hành trình thuận tiện.")}</h2>
                        <button
                            className="dashboard-weather"
                            type="button"
                            onClick={locateCurrentPosition}
                            disabled={locating}
                            aria-label={
                                temperature === null
                                    ? copy("Get local weather and time", "Xem thời tiết và giờ địa phương")
                                    : `${copy("Local temperature", "Nhiệt độ địa phương")} ${temperature} ${copy("degrees Celsius", "độ C")}, ${localTime ?? copy("time loading", "đang tải giờ")}`
                            }
                            title={copy("Use my location for weather and parking", "Dùng vị trí của tôi để xem thời tiết và bãi đỗ")}
                        >
                            <span className="dashboard-weather-icon">
                                <CustomerIcon name="cloud" size={22} />
                            </span>
                            <strong>
                                {weatherState === "ready" &&
                                temperature !== null
                                    ? `${temperature}°C`
                                    : copy("Weather", "Thời tiết")}
                            </strong>
                            <span
                                className="dashboard-weather-divider"
                                aria-hidden="true"
                            />
                            <span>{localTime ?? "--:--"}</span>
                        </button>
                    </div>
                </section>

                <div className="dashboard-container dashboard-content">
                    <section
                        className="dashboard-search-card"
                        aria-label={copy("Find parking", "Tìm bãi đỗ")}
                    >
                        <div
                            className="dashboard-tabs"
                            role="tablist"
                            aria-label={copy("Parking shortcuts", "Lối tắt tìm bãi đỗ")}
                        >
                            <button
                                type="button"
                                role="tab"
                                aria-selected={tab === "find"}
                                className={tab === "find" ? "active" : ""}
                                onClick={() => setTab("find")}
                            >
                                <CustomerIcon name="search" size={23} /> {copy("Find Parking", "Tìm bãi đỗ")}
                            </button>
                            <button
                                type="button"
                                role="tab"
                                aria-selected={tab === "quick"}
                                className={tab === "quick" ? "active" : ""}
                                onClick={() => setTab("quick")}
                            >
                                <CustomerIcon name="clock" size={23} /> {copy("Quick Search", "Tìm nhanh")}
                            </button>
                            <button
                                type="button"
                                role="tab"
                                aria-selected={tab === "saved"}
                                className={tab === "saved" ? "active" : ""}
                                onClick={() => setTab("saved")}
                            >
                                <CustomerIcon name="bookmark" size={23} /> {copy("Saved Places", "Địa điểm đã lưu")}
                            </button>
                        </div>
                        {tab === "saved" ? (
                            <div
                                className="dashboard-saved-options"
                                role="tabpanel"
                            >
                                {savedPlaces.map((place) => (
                                    <button
                                        key={place.name}
                                        type="button"
                                        onClick={() =>
                                            choosePlace(place.detail)
                                        }
                                    >
                                        <span className="dashboard-saved-icon">
                                            <CustomerIcon
                                                name={place.icon}
                                                size={22}
                                            />
                                        </span>
                                        <span>
                                            <strong>{place.name === "Work" ? copy("Work", "Cơ quan") : place.name === "Home" ? copy("Home", "Nhà") : copy("Favorite", "Yêu thích")}</strong>
                                            <small>{place.detail}</small>
                                        </span>
                                        <CustomerIcon name="arrow" size={18} />
                                    </button>
                                ))}
                            </div>
                        ) : tab === "quick" ? (
                            <div
                                className="dashboard-quick-search"
                                role="tabpanel"
                            >
                                <div className="dashboard-quick-heading">
                                    <strong>{copy("Search in one click", "Tìm kiếm chỉ với một lần nhấn")}</strong>
                                    <span>
                                        {copy("Choose a popular destination. We’ll look for parking now.", "Chọn điểm đến phổ biến. Chúng tôi sẽ tìm bãi đỗ ngay.")}
                                    </span>
                                </div>
                                <div className="dashboard-quick-places">
                                    {recentPlaces.map((place) => (
                                        <button
                                            type="button"
                                            key={place}
                                            onClick={() => quickSearch(place)}
                                        >
                                            <span className="dashboard-quick-place-icon">
                                                <CustomerIcon
                                                    name="location"
                                                    size={19}
                                                />
                                            </span>
                                            <strong>{place}</strong>
                                            <CustomerIcon
                                                name="arrow"
                                                size={18}
                                            />
                                        </button>
                                    ))}
                                </div>
                            </div>
                        ) : (
                            <div role="tabpanel">
                                <form
                                    className="dashboard-search-form"
                                    onSubmit={handleSearch}
                                >
                                    <div className="dashboard-search-field dashboard-destination">
                                        <CustomerIcon
                                            name="location"
                                            size={23}
                                        />
                                        <span>
                                            <label
                                                className="dashboard-field-label"
                                                htmlFor="dashboard-destination-input"
                                            >
                                                {copy("Destination", "Điểm đến")}
                                            </label>
                                            <input
                                                id="dashboard-destination-input"
                                                value={destination}
                                                onChange={(event) => {
                                                    setDestination(
                                                        event.target.value,
                                                    );
                                                    setSearchError("");
                                                }}
                                                placeholder={copy("Enter an address or landmark", "Nhập địa chỉ hoặc địa điểm")}
                                                aria-invalid={Boolean(
                                                    searchError,
                                                )}
                                                aria-describedby={
                                                    searchError
                                                        ? "dashboard-search-error"
                                                        : undefined
                                                }
                                            />
                                        </span>
                                        <button
                                            className="dashboard-locate"
                                            type="button"
                                            onClick={locateCurrentPosition}
                                            disabled={locating}
                                            aria-label={copy("Use my current location", "Dùng vị trí hiện tại")}
                                            title={copy("Use my current location", "Dùng vị trí hiện tại")}
                                        >
                                            <CustomerIcon
                                                name="target"
                                                size={20}
                                            />
                                        </button>
                                    </div>
                                    <label className="dashboard-search-field">
                                        <CustomerIcon
                                            name="calendar"
                                            size={23}
                                        />
                                        <span>
                                            <span className="dashboard-field-label">
                                                {copy("Date", "Ngày")}
                                            </span>
                                            <input
                                                type="date"
                                                value={date}
                                                onChange={(event) =>
                                                    setSelectedDate(
                                                        event.target.value,
                                                    )
                                                }
                                            />
                                        </span>
                                    </label>
                                    <label className="dashboard-search-field">
                                        <CustomerIcon name="clock" size={23} />
                                        <span>
                                            <span className="dashboard-field-label">
                                                {copy("Time", "Giờ")}
                                            </span>
                                            <input
                                                type="time"
                                                value={time}
                                                onChange={(event) =>
                                                    setSelectedTime(
                                                        event.target.value,
                                                    )
                                                }
                                            />
                                        </span>
                                    </label>
                                    <div className="dashboard-search-field dashboard-vehicle-field">
                                        <CustomerIcon name="car" size={23} />
                                        <CustomerSearchDropdown
                                            label={copy("My Vehicle", "Xe của tôi")}
                                            value={vehicle}
                                            options={vehicleOptions.map((option) => ({ ...option, label: option.value === "any" ? copy("Any vehicle", "Mọi loại xe") : option.value === "car" ? copy("Car", "Ô tô") : copy("Motorbike", "Xe máy") }))}
                                            onChange={setVehicle}
                                            variant="field"
                                            align="right"
                                        />
                                    </div>
                                    <button
                                        className="dashboard-search-submit"
                                        type="submit"
                                    >
                                        <CustomerIcon name="search" size={24} />
                                        <span>{copy("Find Parking", "Tìm bãi đỗ")}</span>
                                        <CustomerIcon name="arrow" size={21} />
                                    </button>
                                </form>
                                {searchError && (
                                    <p
                                        className="dashboard-search-error"
                                        id="dashboard-search-error"
                                        role="alert"
                                    >
                                        {searchError}
                                    </p>
                                )}
                                {locationMessage && (
                                    <p
                                        className="dashboard-location-message"
                                        role="status"
                                    >
                                        {locationMessage}
                                    </p>
                                )}
                                <div className="dashboard-recent">
                                    <span>
                                        <CustomerIcon
                                            name="history"
                                            size={19}
                                        />
                                        {copy("Suggested places:", "Địa điểm gợi ý:")}
                                    </span>
                                    <div>
                                        {recentPlaces.map((place) => (
                                            <button
                                                type="button"
                                                key={place}
                                                onClick={() =>
                                                    choosePlace(place)
                                                }
                                            >
                                                <CustomerIcon
                                                    name="location"
                                                    size={17}
                                                />
                                                {place}
                                            </button>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        )}
                    </section>

                    <div className="dashboard-cards">
                        <section
                            className="dashboard-info-card dashboard-session"
                            aria-labelledby="dashboard-session-title"
                        >
                            <div className="dashboard-card-head">
                                <div className="dashboard-card-title">
                                    <span className="dashboard-heading-icon green">
                                        <CustomerIcon name="car" size={27} />
                                    </span>
                                    <h2 id="dashboard-session-title">
                                        {copy("Active Parking Session", "Phiên đỗ đang hoạt động")}
                                    </h2>
                                </div>
                                <Link href="/customer/sessions">
                                    {copy("My sessions", "Phiên đỗ của tôi")}{" "}
                                    <CustomerIcon name="arrow" size={17} />
                                </Link>
                            </div>
                            <div className="dashboard-session-body">
                                <ActiveSessionCard />
                            </div>
                        </section>
                        <section
                            className="dashboard-info-card dashboard-booking"
                            id="upcoming-booking"
                            aria-labelledby="dashboard-booking-title"
                        >
                            <div className="dashboard-card-head">
                                <div className="dashboard-card-title">
                                    <span className="dashboard-heading-icon blue">
                                        <CustomerIcon
                                            name="calendar"
                                            size={26}
                                        />
                                    </span>
                                    <h2 id="dashboard-booking-title">
                                        {copy("Upcoming Booking", "Đặt chỗ sắp tới")}
                                    </h2>
                                    <span className="dashboard-status blue">
                                        {copy("Demo data", "Dữ liệu mẫu")}
                                    </span>
                                </div>
                                <Link href="/search">
                                    {copy("Find parking", "Tìm bãi đỗ")}{" "}
                                    <CustomerIcon name="arrow" size={17} />
                                </Link>
                            </div>
                            <div className="dashboard-booking-body">
                                <div className="dashboard-booking-image">
                                    <Image
                                        src="/images/parkwise/dashboard-parking-entrance.webp"
                                        alt="City parking entrance"
                                        fill
                                        sizes="170px"
                                    />
                                </div>
                                <div>
                                    <h3>
                                        {dashboardPreviewData.booking.lotName}
                                    </h3>
                                    <p>
                                        {dashboardPreviewData.booking.location}
                                    </p>
                                </div>
                            </div>
                            <div className="dashboard-booking-facts">
                                <span>
                                    <CustomerIcon name="calendar" size={22} />
                                    <span>
                                        <small>{copy("Date", "Ngày")}</small>
                                        <strong>
                                            {dashboardPreviewData.booking.date}
                                        </strong>
                                    </span>
                                </span>
                                <span>
                                    <CustomerIcon name="clock" size={22} />
                                    <span>
                                        <small>{copy("Time", "Giờ")}</small>
                                        <strong>
                                            {dashboardPreviewData.booking.time}
                                        </strong>
                                    </span>
                                </span>
                                <span>
                                    <CustomerIcon name="car" size={22} />
                                    <span>
                                        <small>{copy("Vehicle", "Xe")}</small>
                                        <strong>
                                            {
                                                dashboardPreviewData.booking
                                                    .vehicle
                                            }
                                        </strong>
                                    </span>
                                </span>
                            </div>
                        </section>
                    </div>

                    <section
                        className="dashboard-places"
                        aria-labelledby="dashboard-places-title"
                    >
                        <div className="dashboard-places-copy">
                            <span
                                className="dashboard-places-star"
                                aria-hidden="true"
                            >
                                ★
                            </span>
                            <div>
                                <h2 id="dashboard-places-title">
                                    {copy("Your Saved Places", "Địa điểm đã lưu")} <small>{copy("Demo data", "Dữ liệu mẫu")}</small>
                                </h2>
                                <p>
                                    {copy("Quick access to your favorite locations and parking recommendations.", "Truy cập nhanh các địa điểm yêu thích và gợi ý bãi đỗ.")}
                                </p>
                                <button
                                    type="button"
                                    onClick={() => {
                                        setTab("saved");
                                        document
                                            .querySelector(
                                                ".dashboard-search-card",
                                            )
                                            ?.scrollIntoView({
                                                behavior: "smooth",
                                            });
                                    }}
                                >
                                    {copy("View All Places", "Xem mọi địa điểm")}{" "}
                                    <CustomerIcon name="arrow" size={16} />
                                </button>
                            </div>
                        </div>
                        <div className="dashboard-map" aria-hidden="true" />
                        <div className="dashboard-map-pins">
                            {savedPlaces.map((place) => (
                                <button
                                    key={place.name}
                                    type="button"
                                    onClick={() => {
                                        choosePlace(place.detail);
                                        document
                                            .querySelector(
                                                ".dashboard-search-card",
                                            )
                                            ?.scrollIntoView({
                                                behavior: "smooth",
                                            });
                                    }}
                                >
                                    <span className="dashboard-pin-icon">
                                        <CustomerIcon
                                            name={place.icon}
                                            size={19}
                                        />
                                    </span>
                                    <span>
                                        <strong>{place.name === "Work" ? copy("Work", "Cơ quan") : place.name === "Home" ? copy("Home", "Nhà") : copy("Favorite", "Yêu thích")}</strong>
                                        <small>{place.detail}</small>
                                    </span>
                                </button>
                            ))}
                        </div>
                    </section>
                </div>
            </main>
            <CustomerFooter />
        </div>
    );
}
