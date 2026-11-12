"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { CircleAlert, Layers, Shield, Ticket, UserRound } from "lucide-react";
import { getAuthSessionSnapshot } from "@/features/auth/authSession";
import {
    VNEID_DEMO_PROFILES,
    VNEID_PROFILE_RETURN,
    VNEID_SHARE_KEY,
} from "./vneidDemoSession";

export function VneidConsentPage({
    fontClassName = "",
}: {
    fontClassName?: string;
}) {
    const router = useRouter();
    const [seedId, setSeedId] = useState(VNEID_DEMO_PROFILES[0]?.cccd ?? "");
    const [consent, setConsent] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const profile = VNEID_DEMO_PROFILES.find((item) => item.cccd === seedId);

    function cancel() {
        try {
            sessionStorage.removeItem(VNEID_SHARE_KEY);
        } catch {
            /* No data is shared. */
        }
        router.replace(VNEID_PROFILE_RETURN);
    }

    function confirm(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        if (!consent || !profile || busy) return;
        try {
            sessionStorage.setItem(
                VNEID_SHARE_KEY,
                JSON.stringify({
                    version: 1,
                    consent: true,
                    seedId: profile.cccd,
                    accountId: getAuthSessionSnapshot()?.account.id ?? null,
                    createdAt: Date.now(),
                }),
            );
            setBusy(true);
            router.replace(`${VNEID_PROFILE_RETURN}?vneid=shared`);
        } catch {
            setError(
                "Không thể chia sẻ dữ liệu mẫu. Vui lòng cho phép lưu trữ trên trình duyệt và thử lại.",
            );
        }
    }

    return (
        <main lang="vi" className={`vneid-consent-page ${fontClassName}`}>
            <header className="vneid-consent-banner">
                <div className="vneid-consent-brand">
                    <span className="vneid-consent-emblem" aria-hidden="true">
                        <Shield />
                        <span>VNeID</span>
                    </span>
                    <div>
                        <p className="vneid-consent-brand-name">
                            VNeID <span>demo</span>
                        </p>
                        <p className="vneid-consent-brand-caption">
                            Mô phỏng liên kết định danh điện tử
                        </p>
                        <span className="vneid-consent-demo-tag">
                            DEMO / SANDBOX
                        </span>
                    </div>
                </div>
                <svg
                    className="vneid-consent-landscape"
                    viewBox="0 0 600 170"
                    fill="none"
                    aria-hidden="true"
                >
                    <g stroke="currentColor" strokeWidth="2">
                        <path d="M220 150V70h120v80M210 70h140l-20-18H230zM233 52V38h94v14M220 38h120l-20-16h-80zM247 150v-40a33 33 0 0166 0v40M245 76v20m70-20v20M280 22V10M390 150l16-93h24l16 93M401 57V45h34v12M418 45V5m0 3h30v18h-30M478 150V97h43v53M540 150V64h44v86M0 153h600" />
                        <path d="M110 150v-24m0 4c-30-2-45-20-45-36 25 0 41 10 45 30m0 6c30-2 45-20 45-36-25 0-41 10-45 30m0 1c-17-15-20-37 0-55 20 18 17 40 0 55" />
                        <path d="M487 107h5m10 0h5m-20 12h5m10 0h5m-20 12h5m10 0h5M550 76h6m10 0h6m-22 12h6m10 0h6m-22 12h6m10 0h6m-22 12h6m10 0h6m-22 12h6m10 0h6" />
                    </g>
                </svg>
                <svg
                    className="vneid-consent-waves"
                    viewBox="0 0 1440 100"
                    preserveAspectRatio="none"
                    aria-hidden="true"
                >
                    <path
                        d="M0 60C180 0 260 140 480 50S760 10 940 60 1190 0 1440 50V100H0Z"
                        fill="currentColor"
                    />
                    <path
                        d="M0 85C200 20 300 140 510 65S790 25 970 75 1220 20 1440 65"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                    />
                </svg>
            </header>

            <section
                className="vneid-consent-card"
                aria-labelledby="vneid-consent-title"
            >
                <div className="vneid-consent-shield" aria-hidden="true">
                    <Shield />
                    <UserRound />
                </div>
                <h1 id="vneid-consent-title">Liên kết định danh điện tử</h1>
                <p className="vneid-consent-intro">
                    ParkWise đang yêu cầu liên kết với tài khoản định danh điện
                    tử (VNeID) để chia sẻ dữ liệu phục vụ trải nghiệm (mô
                    phỏng).
                </p>
                <form onSubmit={confirm}>
                    <dl className="vneid-consent-details">
                        <div>
                            <span className="vneid-consent-row-icon">
                                <UserRound aria-hidden="true" />
                            </span>
                            <div>
                                <dt>
                                    <label htmlFor="vneid-demo-account">
                                        Tài khoản
                                    </label>
                                </dt>
                                <dd>
                                    <select
                                        id="vneid-demo-account"
                                        value={seedId}
                                        disabled={busy}
                                        onChange={(event) => {
                                            setSeedId(event.target.value);
                                            setConsent(false);
                                            setError("");
                                        }}
                                    >
                                        {VNEID_DEMO_PROFILES.map((item) => (
                                            <option
                                                key={item.cccd}
                                                value={item.cccd}
                                            >
                                                {item.fullName} — Người dùng thử
                                                nghiệm
                                            </option>
                                        ))}
                                    </select>
                                </dd>
                            </div>
                        </div>
                        <div>
                            <span className="vneid-consent-row-icon">
                                <Ticket aria-hidden="true" />
                            </span>
                            <div>
                                <dt>Mã giao dịch</dt>
                                <dd>
                                    DEMO-001{" "}
                                    <span className="vneid-consent-local-tag">
                                        Mô phỏng cục bộ
                                    </span>
                                </dd>
                            </div>
                        </div>
                        <div>
                            <span className="vneid-consent-row-icon">
                                <Layers aria-hidden="true" />
                            </span>
                            <div>
                                <dt>Phạm vi</dt>
                                <dd>Chia sẻ dữ liệu mô phỏng</dd>
                            </div>
                        </div>
                    </dl>
                    {profile && (
                        <section
                            className="vneid-consent-preview"
                            aria-labelledby="vneid-preview-title"
                        >
                            <div className="vneid-consent-preview-heading">
                                <h2 id="vneid-preview-title">
                                    Dữ liệu mẫu sẽ chia sẻ
                                </h2>
                                <span>Chưa xác thực</span>
                            </div>
                            <dl>
                                <div>
                                    <dt>Họ và tên</dt>
                                    <dd>{profile.fullName}</dd>
                                </div>
                                <div>
                                    <dt>Ngày sinh</dt>
                                    <dd>{profile.dobDisplay}</dd>
                                </div>
                                <div>
                                    <dt>Số CCCD mẫu</dt>
                                    <dd>{profile.cccd}</dd>
                                </div>
                                <div>
                                    <dt>Ngày cấp</dt>
                                    <dd>{profile.issueDate}</dd>
                                </div>
                                <div className="vneid-consent-preview-address">
                                    <dt>Địa chỉ</dt>
                                    <dd>{profile.residence}</dd>
                                </div>
                            </dl>
                        </section>
                    )}
                    <p className="vneid-consent-scope">
                        Thông tin mẫu: họ tên, ngày sinh, số CCCD, ngày cấp và
                        địa chỉ. Chưa kết nối trực tuyến với VNeID /
                        HiTechCloud.
                    </p>
                    <label className="vneid-consent-checkbox">
                        <input
                            type="checkbox"
                            checked={consent}
                            disabled={busy}
                            onChange={(event) =>
                                setConsent(event.target.checked)
                            }
                        />
                        <span>Tôi đồng ý chia sẻ dữ liệu thử nghiệm.</span>
                    </label>
                    {error && (
                        <p className="vneid-consent-error" role="alert">
                            {error}
                        </p>
                    )}
                    <div className="vneid-consent-actions">
                        <button type="button" onClick={cancel} disabled={busy}>
                            Hủy
                        </button>
                        <button
                            type="submit"
                            disabled={!consent || !profile || busy}
                        >
                            {busy
                                ? "Đang quay về ParkWise…"
                                : "Xác nhận liên kết"}
                        </button>
                    </div>
                    <p className="vneid-consent-warning">
                        <CircleAlert aria-hidden="true" />
                        <strong>
                            BẢN MÔ PHỎNG – KHÔNG CÓ GIÁ TRỊ XÁC THỰC
                        </strong>
                    </p>
                </form>
            </section>
            <footer className="vneid-consent-footer">
                ParkWise Demo · Chỉ sử dụng hồ sơ mẫu để trải nghiệm
            </footer>
        </main>
    );
}
