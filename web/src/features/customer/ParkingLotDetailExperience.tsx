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
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { ParkingLayoutView } from "./ParkingLayoutView";
import { CustomerLiveParkingLayout } from "./CustomerLiveParkingLayout";
import { ParkingLotBookingDialog } from "./booking/components/ParkingLotBookingDialog";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { getMyVehicles, type RegisteredVehicle } from "./myVehiclesApi";
import { isFutureBookingStart } from "./bookingDateTime";
import { useParkingRealtime } from "@/features/parking/useParkingRealtime";
import {
    estimateLotBookingPricing,
    type DailyOpeningHours,
    type LotPrice,
} from "./parkingLotApi";

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
    pricing?: LotPrice[];
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
const vehicleLabelsVi: Record<string, string> = {
    car: "Ô tô",
    motorbike: "Xe máy",
    ev: "Xe điện",
    large: "Xe cỡ lớn",
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
const amenityLabelsVi: Record<string, string> = {
    covered: "Có mái che",
    "always-open": "Mở cửa 24/7",
    cctv: "Camera an ninh",
    "ev-charging": "Sạc xe điện",
    restrooms: "Nhà vệ sinh",
    lighting: "Chiếu sáng",
    accessible: "Lối đi cho người khuyết tật",
    "security-staff": "Nhân viên bảo vệ",
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
const weekdayNamesVi = [
    "Chủ nhật",
    "Thứ hai",
    "Thứ ba",
    "Thứ tư",
    "Thứ năm",
    "Thứ sáu",
    "Thứ bảy",
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
    const { language, copy } = useCustomerLanguage();
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
    const [realtimeSpaces, setRealtimeSpaces] = useState<number | null>(null);
    const { isConnected } = useParkingRealtime({
        lotId: lot.id,
        onLotOccupancyChanged: (event) => {
            setRealtimeSpaces(event.availableCarSlots);
        },
    });
    const photos = lot.photos.length ? lot.photos : [lot.image];
    const directionsHref =
        lot.latitude !== undefined && lot.longitude !== undefined
            ? `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(`${lot.latitude},${lot.longitude}`)}`
            : null;
    const selectedVehicle = vehicles.find(
        (item) => item.vehicleId === vehicleId,
    );
    const bookingPricing = estimateLotBookingPricing(
        lot.pricing,
        duration,
        selectedVehicle?.vehicleType,
        lot.hourlyPrice,
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
                const isEligible = (item: RegisteredVehicle) =>
                    !lot.compatible?.length ||
                    lot.compatible.includes(item.vehicleType.toLowerCase()) ||
                    (item.vehicleType === "Car" &&
                        lot.compatible.some(
                            (kind) => kind === "ev" || kind === "large",
                        ));
                const defaultVehicle = result.vehicles.find(
                    (item) => item.isDefault && isEligible(item),
                );
                const eligible =
                    defaultVehicle ?? result.vehicles.find(isEligible);
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
            lang={language}
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
                                        {copy("Open now", "Đang mở cửa")}
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
                                        {copy("View on map", "Xem trên bản đồ")}{" "}
                                        <ArrowRight size={15} />
                                    </a>
                                )}
                            </div>
                            <div className="lot-detail-vehicles">
                                {lot.rating !== undefined && (
                                    <span className="lot-detail-rating">
                                        <Star size={17} fill="currentColor" />
                                        <strong>{lot.rating.toFixed(1)}</strong>
                                        <small>
                                            ({lot.reviewCount}{" "}
                                            {copy("reviews", "đánh giá")})
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
                                        {language === "vi"
                                            ? (vehicleLabelsVi[item] ?? item)
                                            : (vehicleLabels[item] ?? item)}
                                    </span>
                                ))}
                            </div>
                        </header>

                        <div
                            className="lot-detail-metrics"
                            aria-label={copy(
                                "Parking summary",
                                "Tóm tắt bãi đỗ",
                            )}
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
                                            ? copy(
                                                  "from your location",
                                                  "từ vị trí của bạn",
                                              )
                                            : copy(
                                                  "distance unavailable",
                                                  "chưa có khoảng cách",
                                              )}
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
                                            ? copy(
                                                  "travel time unavailable",
                                                  "chưa có thời gian di chuyển",
                                              )
                                            : copy(
                                                  "estimated travel time",
                                                  "thời gian di chuyển dự kiến",
                                              )}
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
                                            ? copy(
                                                  "current price",
                                                  "giá hiện tại",
                                              )
                                            : copy(
                                                  "no price set yet",
                                                  "chưa có giá",
                                              )}
                                    </small>
                                </span>
                            </div>
                            <div>
                                <CarFront size={21} />
                                <span>
                                    <strong>
                                        {realtimeSpaces !== null
                                            ? `${realtimeSpaces} slots`
                                            : (lot.spacesText ??
                                              (lot.spaces === undefined
                                                  ? "—"
                                                  : lot.spaces))}
                                    </strong>
                                    <small
                                        style={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: "4px",
                                        }}
                                    >
                                        {isConnected && (
                                            <span
                                                style={{
                                                    display: "inline-block",
                                                    width: "6px",
                                                    height: "6px",
                                                    borderRadius: "50%",
                                                    backgroundColor: "#10b981",
                                                    boxShadow:
                                                        "0 0 6px #10b981",
                                                }}
                                            />
                                        )}
                                        {realtimeSpaces !== null
                                            ? "Real-time live"
                                            : lot.spacesText
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
                                        alt={lot.photos.length ? `Parking view ${photoIndex + 1}` : "Illustration of a parking facility"}
                                        fill
                                        sizes="(max-width: 900px) 100vw, 600px"
                                        priority
                                    />
                                    <span className="lot-detail-gallery-count">
                                        <Images size={14} aria-hidden="true" />{" "}
                                        {lot.photos.length
                                            ? `${copy("View all photos", "Xem tất cả ảnh")} (${lot.photos.length})`
                                            : copy("Illustration · No lot photos published", "Ảnh minh họa · Bãi chưa cung cấp ảnh")}
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
                                aria-label={copy(
                                    "About this parking lot",
                                    "Về bãi đỗ này",
                                )}
                            >
                                <h2>
                                    {copy(
                                        "About this parking lot",
                                        "Về bãi đỗ này",
                                    )}
                                </h2>
                                <p>
                                    {lot.isPreview
                                        ? copy(
                                              `${lot.name} is a modern parking facility in the heart of District 1, Ho Chi Minh City. Conveniently situated near Nguyen Hue Walking Street, shopping centers, and business districts.`,
                                              `${lot.name} là bãi đỗ hiện đại tại trung tâm Quận 1, TP. Hồ Chí Minh, gần phố đi bộ Nguyễn Huệ và các trung tâm mua sắm.`,
                                          )
                                        : copy(
                                               `Approved ${lot.type.toLowerCase()} parking at ${lot.address}. Opening hours, amenities and payment methods appear when the operator provides them.`,
                                               `Bãi đỗ đã được duyệt tại ${lot.address}. Giờ mở cửa, tiện ích và phương thức thanh toán sẽ hiển thị khi đơn vị vận hành cung cấp.`,
                                          )}
                                </p>
                                <div className="lot-detail-info-split">
                                    <div>
                                        <h3>
                                            <Clock3 size={18} />{" "}
                                            {copy(
                                                "Opening hours",
                                                "Giờ mở cửa",
                                            )}
                                        </h3>
                                        {lot.openingHours?.length ? (
                                            <details className="lot-detail-hours-details">
                                                <summary>
                                                    {copy(
                                                        "View weekly schedule",
                                                        "Xem lịch trong tuần",
                                                    )}
                                                </summary>
                                                {lot.openingHours.map((day) => (
                                                    <div key={day.dayOfWeek}>
                                                        <span>
                                                            {
                                                                (language ===
                                                                "vi"
                                                                    ? weekdayNamesVi
                                                                    : weekdayNames)[
                                                                    day
                                                                        .dayOfWeek
                                                                ]
                                                            }
                                                        </span>
                                                        <strong>
                                                            {copy(
                                                                hoursLabel(day),
                                                                day.isClosed
                                                                    ? "Đóng cửa"
                                                                    : day.is24Hours
                                                                      ? "Mở cửa 24 giờ"
                                                                      : hoursLabel(
                                                                            day,
                                                                        ).replace(
                                                                            " (next day)",
                                                                            " (hôm sau)",
                                                                        ),
                                                            )}
                                                        </strong>
                                                    </div>
                                                ))}
                                                <small>
                                                    {copy(
                                                        "Vietnam time (UTC+7)",
                                                        "Giờ Việt Nam (UTC+7)",
                                                    )}
                                                </small>
                                            </details>
                                        ) : (
                                            <p>
                                                {lot.isPreview &&
                                                lot.amenities?.includes(
                                                    "always-open",
                                                )
                                                    ? copy(
                                                          "24/7 access",
                                                          "Mở cửa 24/7",
                                                      )
                                                    : copy(
                                                           "Ask the operator for opening hours",
                                                           "Liên hệ đơn vị vận hành để biết giờ mở cửa",
                                                      )}
                                            </p>
                                        )}
                                    </div>
                                    <div>
                                        <h3>
                                            <Layers3 size={18} />{" "}
                                            {copy("Amenities", "Tiện ích")}
                                        </h3>
                                        {lot.amenities?.length ? (
                                            <ul className="lot-detail-amenities">
                                                {lot.amenities.map((item) => (
                                                    <li key={item}>
                                                        {amenityIcon(item)}
                                                        {language === "vi"
                                                            ? (amenityLabelsVi[
                                                                  item
                                                              ] ?? item)
                                                            : (amenityLabels[
                                                                  item
                                                              ] ?? item)}
                                                    </li>
                                                ))}
                                            </ul>
                                        ) : (
                                            <p>
                                                {copy(
                                                     "No amenities listed by the operator",
                                                     "Đơn vị vận hành chưa cung cấp tiện ích",
                                                )}
                                            </p>
                                        )}
                                    </div>
                                </div>
                                <div className="lot-detail-info-split lot-detail-info-bottom">
                                    <div>
                                        <h3>
                                            <CreditCard size={18} />{" "}
                                            {copy(
                                                "Payment methods",
                                                "Phương thức thanh toán",
                                            )}
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
                                            <p>
                                                {copy(
                                                     "Ask the operator",
                                                     "Liên hệ đơn vị vận hành",
                                                )}
                                            </p>
                                        )}
                                    </div>
                                    <div>
                                        <h3>
                                            <ShieldCheck size={18} />{" "}
                                            {copy(
                                                "Parking rules",
                                                "Quy định đỗ xe",
                                            )}
                                        </h3>
                                        {lot.isPreview ? (
                                            <ul className="lot-detail-rules">
                                                <li>
                                                    <Check size={14} />{" "}
                                                    {copy(
                                                        "Follow staff instructions",
                                                        "Làm theo hướng dẫn của nhân viên",
                                                    )}
                                                </li>
                                                <li>
                                                    <Check size={14} />{" "}
                                                    {copy(
                                                        "Park within marked spaces",
                                                        "Đỗ xe đúng vị trí đã kẻ",
                                                    )}
                                                </li>
                                                <li>
                                                    <Check size={14} />{" "}
                                                    {copy(
                                                        "Keep valuables secure",
                                                        "Tự bảo quản tài sản có giá trị",
                                                    )}
                                                </li>
                                            </ul>
                                        ) : (
                                            <p>
                                                {copy(
                                                    "Ask the parking operator for current rules.",
                                                    "Liên hệ đơn vị vận hành để biết quy định hiện tại.",
                                                )}
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
                        {!lot.isPreview && (
                            <CustomerLiveParkingLayout lotId={lot.id} />
                        )}
                    </div>

                    <aside
                        className="lot-detail-booking-card"
                        aria-labelledby="lot-detail-booking-title"
                    >
                        <div className="lot-detail-booking-heading">
                            <h2 id="lot-detail-booking-title">
                                {copy("Book this parking", "Đặt chỗ đỗ xe")}
                            </h2>
                        </div>
                        <div className="lot-detail-booking-fields">
                            <label>
                                <span>
                                    <CarFront size={21} />
                                </span>
                                <small>
                                    {copy("Your vehicle", "Xe của bạn")}
                                </small>
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
                                                ? copy(
                                                      "Sign in to choose a vehicle",
                                                      "Đăng nhập để chọn xe",
                                                  )
                                                : vehiclesState === "loading"
                                                  ? copy(
                                                        "Loading vehicles",
                                                        "Đang tải xe",
                                                    )
                                                  : copy(
                                                        "Add a vehicle to continue",
                                                        "Thêm xe để tiếp tục",
                                                    )}
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
                                        ? copy(
                                              "Manage my vehicles",
                                              "Quản lý xe của tôi",
                                          )
                                        : copy(
                                              "Sign in to use My Vehicles",
                                              "Đăng nhập để dùng mục Xe của tôi",
                                          )}
                                </Link>
                            </div>
                            <label>
                                <span>
                                    <CalendarDays size={21} />
                                </span>
                                <small>{copy("Date", "Ngày")}</small>
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
                                <small>{copy("Time", "Giờ")}</small>
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
                                <small>{copy("Duration", "Thời lượng")}</small>
                                <select
                                    value={duration}
                                    onChange={(event) =>
                                        setDuration(Number(event.target.value))
                                    }
                                >
                                    {[1, 2, 3, 4, 6, 8].map((hours) => (
                                        <option key={hours} value={hours}>
                                            {hours}{" "}
                                            {copy(
                                                hours === 1 ? "hour" : "hours",
                                                "giờ",
                                            )}
                                        </option>
                                    ))}
                                </select>
                            </label>
                        </div>
                        <div className="lot-detail-booking-total">
                            <div>
                                <span>
                                    {bookingPricing?.isPerEntry
                                        ? copy(
                                              "Per-entry rate",
                                              "Giá theo lượt",
                                          )
                                        : copy("Hourly rate", "Giá theo giờ")}
                                </span>
                                <strong>
                                    {bookingPricing === null
                                        ? copy("Unavailable", "Chưa có")
                                        : formatMoney(bookingPricing.rateValue)}
                                </strong>
                            </div>
                            <div>
                                <span>{copy("Duration", "Thời lượng")}</span>
                                <strong>
                                    {duration}{" "}
                                    {copy(
                                        duration === 1 ? "hour" : "hours",
                                        "giờ",
                                    )}
                                </strong>
                            </div>
                            <div>
                                <span>
                                    {copy(
                                        "Estimated total",
                                        "Tổng tiền dự kiến",
                                    )}
                                </span>
                                <strong>
                                    {bookingPricing === null
                                        ? copy("Unavailable", "Chưa có")
                                        : formatMoney(
                                              bookingPricing.estimatedTotal,
                                          )}
                                </strong>
                            </div>
                        </div>
                        {lot.isPreview && (
                            <div className="lot-detail-booking-notice">
                                <CarFront size={20} aria-hidden="true" />
                                <span>
                                    <strong>
                                        {lot.spaces}{" "}
                                        {copy(
                                            "spaces available",
                                            "chỗ còn trống",
                                        )}
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
                            {copy("Book Now", "Đặt ngay")}{" "}
                            <ArrowRight size={18} />
                        </button>
                    </aside>
                </div>

                <Link href="/search" className="lot-detail-bottom-back">
                    <ArrowLeft size={16} />{" "}
                    {copy("Back to Find Parking", "Quay lại tìm bãi đỗ")}
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
