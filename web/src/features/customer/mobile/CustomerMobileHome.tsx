"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import Image from "next/image";
import { parkwiseAssets } from "@/features/parkwise/parkwiseAssets";
import { ParkWiseAction } from "@/features/parkwise/ParkWiseAction";
import { ParkWiseIconTile } from "@/features/parkwise/ParkWiseIconTile";
import { useRouter } from "next/navigation";
import {
    ArrowUpRight,
    CarFront,
    ChevronRight,
    CircleParking,
    MapPin,
    Search,
    ShieldCheck,
    Wallet,
} from "lucide-react";
import { CustomerHeader } from "../CustomerShell";
import { ActiveSessionCard } from "../CustomerSessions";
import {
    getApprovedParkingLots,
    type ParkingLotSummary,
} from "../parkingLotApi";
import { useCustomerLanguage } from "../i18n/customerLanguage";

export function CustomerMobileHome({
    fontClassName,
    displayName,
}: {
    fontClassName: string;
    displayName?: string;
}) {
    const { copy } = useCustomerLanguage();
    const router = useRouter();
    const [destination, setDestination] = useState("");
    const [lots, setLots] = useState<ParkingLotSummary[]>([]);
    const [state, setState] = useState<"loading" | "ready" | "error">(
        "loading",
    );
    useEffect(() => {
        const controller = new AbortController();
        getApprovedParkingLots(undefined, controller.signal)
            .then((items) => {
                setLots(items.slice(0, 4));
                setState("ready");
            })
            .catch(() => {
                if (!controller.signal.aborted) setState("error");
            });
        return () => controller.abort();
    }, []);
    function search(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        router.push(
            destination.trim()
                ? `/search?${new URLSearchParams({ destination: destination.trim() })}`
                : "/search",
        );
    }
    return (
        <div className={`parkwise-page pw-app-home ${fontClassName}`}>
            <CustomerHeader active="home" />
            <main className="pw-app-home-main">
                <section className="pw-app-welcome">
                    <Image
                        src={parkwiseAssets.parkingHero}
                        alt=""
                        fill
                        priority
                        sizes="(max-width: 767px) 100vw, 1px"
                        className="pw-app-welcome-image"
                    />
                    <div className="pw-app-greeting">
                        <div>
                            <p>
                                {copy("Hello", "Xin chào")}
                                {displayName ? `, ${displayName}` : ""}
                            </p>
                            <h2>
                                {copy(
                                    "Where are you parking today?",
                                    "Hôm nay, bạn đỗ xe ở đâu?",
                                )}
                            </h2>
                        </div>
                    </div>
                    <form
                        className="pw-app-search"
                        onSubmit={search}
                        role="search"
                    >
                        <Search size={22} aria-hidden="true" />
                        <input
                            value={destination}
                            onChange={(event) =>
                                setDestination(event.target.value)
                            }
                            aria-label={copy(
                                "Parking destination",
                                "Địa điểm cần tìm bãi",
                            )}
                            placeholder={copy(
                                "Enter an address or place",
                                "Nhập địa chỉ hoặc địa điểm",
                            )}
                        />
                        <ParkWiseAction
                            type="submit"
                            aria-label={copy("Find parking", "Tìm bãi đỗ")}
                        >
                            <ArrowUpRight size={22} aria-hidden="true" />
                        </ParkWiseAction>
                    </form>
                    <p className="pw-app-search-hint">
                        <ShieldCheck size={15} aria-hidden="true" />
                        {copy(
                            "Find an approved lot before you arrive",
                            "Tìm bãi đã duyệt trước khi bạn đến",
                        )}
                    </p>
                </section>
                <section
                    className="pw-app-services"
                    aria-label={copy("Parking services", "Tiện ích đỗ xe")}
                >
                    <Link href="/search" className="pw-app-service-primary">
                        <div>
                            <h2>{copy("Find parking", "Tìm bãi đỗ")}</h2>
                            <p>
                                {copy(
                                    "A space for your next stop",
                                    "Chọn chỗ cho điểm đến tiếp theo",
                                )}
                            </p>
                        </div>
                        <CircleParking
                            size={58}
                            strokeWidth={1.3}
                            aria-hidden="true"
                        />
                        <span>
                            <ArrowUpRight size={19} aria-hidden="true" />
                        </span>
                    </Link>
                    <Link
                        href="/customer/vehicles"
                        className="pw-app-service-secondary parkwise-tone"
                        data-tone="vehicles"
                    >
                        <CarFront size={29} aria-hidden="true" />
                        <h2>{copy("My vehicles", "Xe của tôi")}</h2>
                        <p>
                            {copy(
                                "Ready for your journey",
                                "Sẵn sàng cho hành trình",
                            )}
                        </p>
                        <ChevronRight size={18} aria-hidden="true" />
                    </Link>
                </section>
                <nav
                    className="pw-app-tools"
                    aria-label={copy("Quick access", "Truy cập nhanh")}
                >
                    <Link href="/customer/wallet">
                        <ParkWiseIconTile tone="wallet">
                            <Wallet size={24} aria-hidden="true" />
                        </ParkWiseIconTile>
                        {copy("Wallet", "Ví của tôi")}
                    </Link>
                    <Link href="/customer/bookings">
                        <ParkWiseIconTile tone="booking">
                            <CircleParking size={24} aria-hidden="true" />
                        </ParkWiseIconTile>
                        {copy("Bookings", "Đặt chỗ")}
                    </Link>
                    <Link href="/customer/sessions">
                        <ParkWiseIconTile tone="parking">
                            <CarFront size={24} aria-hidden="true" />
                        </ParkWiseIconTile>
                        {copy("Parking now", "Đang đỗ")}
                    </Link>
                    <Link href="/customer/profile">
                        <ParkWiseIconTile tone="account">
                            <ShieldCheck size={24} aria-hidden="true" />
                        </ParkWiseIconTile>
                        {copy("Account", "Tài khoản")}
                    </Link>
                </nav>
                <section className="pw-app-section">
                    <div className="pw-app-section-heading">
                        <h2>
                            {copy("Your parking session", "Phiên đỗ của bạn")}
                        </h2>
                        <Link href="/customer/sessions">
                            {copy("View", "Xem")}
                            <ChevronRight size={16} aria-hidden="true" />
                        </Link>
                    </div>
                    <div className="pw-app-live-card">
                        <ActiveSessionCard />
                    </div>
                </section>
                <section className="pw-app-section">
                    <div className="pw-app-section-heading">
                        <h2>{copy("Explore parking", "Khám phá bãi đỗ")}</h2>
                        <Link href="/search">
                            {copy("View all", "Tất cả")}
                            <ChevronRight size={16} aria-hidden="true" />
                        </Link>
                    </div>
                    {state === "loading" && (
                        <p className="pw-app-state" role="status">
                            {copy("Loading parking lots…", "Đang tải bãi đỗ…")}
                        </p>
                    )}
                    {state === "error" && (
                        <div className="pw-app-state" role="alert">
                            <p>
                                {copy(
                                    "Parking lots could not be loaded.",
                                    "Chưa thể tải danh sách bãi đỗ.",
                                )}
                            </p>
                            <Link href="/search">
                                {copy("Open search", "Mở trang tìm bãi")}
                                <ArrowUpRight size={16} />
                            </Link>
                        </div>
                    )}
                    {state === "ready" && lots.length === 0 && (
                        <p className="pw-app-state">
                            {copy(
                                "No approved parking lots are available yet.",
                                "Chưa có bãi đỗ đã duyệt để hiển thị.",
                            )}
                        </p>
                    )}
                    <div className="pw-app-lot-list">
                        {lots.map((lot) => (
                            <Link
                                key={lot.id}
                                href={`/parking-lots/${lot.id}`}
                                className="pw-app-lot"
                            >
                                <span
                                    className="pw-app-lot-symbol"
                                    aria-hidden="true"
                                >
                                    <CircleParking size={28} />
                                </span>
                                <div>
                                    <h3>{lot.name}</h3>
                                    <p>
                                        <MapPin size={13} aria-hidden="true" />
                                        {lot.address}
                                    </p>
                                </div>
                                <ChevronRight size={19} aria-hidden="true" />
                            </Link>
                        ))}
                    </div>
                </section>
            </main>
        </div>
    );
}
