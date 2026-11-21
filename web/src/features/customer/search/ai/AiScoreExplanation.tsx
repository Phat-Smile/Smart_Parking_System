"use client";

import { useEffect, useRef } from "react";
import {
    Info,
    X,
    Navigation,
    DollarSign,
    CheckCircle2,
    AlertTriangle,
    ShieldCheck,
} from "lucide-react";
import "./ai-recommendation.css";

export interface AiScoreExplanationProps {
    isOpen: boolean;
    onClose: () => void;
    title?: string;
}

export function AiScoreExplanation({
    isOpen,
    onClose,
    title = "Cách AI tính điểm và đề xuất bãi đỗ xe",
}: AiScoreExplanationProps) {
    const dialogRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!isOpen) return;

        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") {
                onClose();
            }
        };

        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [isOpen, onClose]);

    if (!isOpen) return null;

    return (
        <div
            className="ai-modal-backdrop"
            onClick={(e) => {
                if (e.target === e.currentTarget) {
                    onClose();
                }
            }}
            data-testid="ai-score-explanation-backdrop"
        >
            <div
                ref={dialogRef}
                className="ai-modal-dialog"
                role="dialog"
                aria-modal="true"
                aria-labelledby="ai-score-explanation-title"
                tabIndex={-1}
            >
                <div className="ai-modal-header">
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: "var(--pw-space-xs)",
                        }}
                    >
                        <span className="ai-panel-icon" aria-hidden="true">
                            <Info size={18} />
                        </span>
                        <h2
                            id="ai-score-explanation-title"
                            className="ai-modal-title"
                        >
                            {title}
                        </h2>
                    </div>
                    <button
                        type="button"
                        className="ai-modal-close-btn"
                        onClick={onClose}
                        aria-label="Đóng bảng giải thích"
                    >
                        <X size={20} />
                    </button>
                </div>

                <div className="ai-modal-body">
                    <p style={{ margin: 0 }}>
                        ParkWise sử dụng mô hình tối ưu đa tiêu chí
                        (Multi-Criteria Utility Scoring) theo tiêu chuẩn BR-08
                        để lựa chọn và xếp hạng Top 3 bãi đỗ xe tốt nhất cho
                        chuyến đi của bạn.
                    </p>

                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: "var(--pw-space-sm)",
                        }}
                    >
                        {/* 40% Distance & Travel Time */}
                        <div
                            style={{
                                padding: "var(--pw-space-sm)",
                                backgroundColor: "var(--pw-surface-subtle)",
                                borderRadius: "var(--pw-radius-md)",
                                border: "1px solid var(--pw-border-subtle)",
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                    marginBottom: "4px",
                                }}
                            >
                                <span
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: "6px",
                                        fontWeight: 700,
                                        color: "var(--pw-text-primary)",
                                    }}
                                >
                                    <Navigation
                                        size={16}
                                        color="var(--pw-brand-primary)"
                                    />
                                    Khoảng cách & thời gian di chuyển
                                </span>
                                <span
                                    className="ai-panel-badge"
                                    style={{
                                        backgroundColor:
                                            "var(--pw-status-info-bg)",
                                        color: "var(--pw-brand-primary)",
                                        fontWeight: 700,
                                    }}
                                >
                                    40% tổng điểm
                                </span>
                            </div>
                            <p
                                style={{
                                    margin: 0,
                                    fontSize: "0.8rem",
                                    color: "var(--pw-text-secondary)",
                                }}
                            >
                                Ưu tiên các bãi xe có thời gian lái xe ngắn nhất
                                và cự ly gần nhất tới điểm đến để tiết kiệm thời
                                gian cho bạn.
                            </p>
                        </div>

                        {/* 40% Pricing */}
                        <div
                            style={{
                                padding: "var(--pw-space-sm)",
                                backgroundColor: "var(--pw-surface-subtle)",
                                borderRadius: "var(--pw-radius-md)",
                                border: "1px solid var(--pw-border-subtle)",
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                    marginBottom: "4px",
                                }}
                            >
                                <span
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: "6px",
                                        fontWeight: 700,
                                        color: "var(--pw-text-primary)",
                                    }}
                                >
                                    <DollarSign
                                        size={16}
                                        color="var(--pw-status-success)"
                                    />
                                    Mức giá đỗ xe
                                </span>
                                <span
                                    className="ai-panel-badge"
                                    style={{
                                        backgroundColor:
                                            "var(--pw-status-success-bg)",
                                        color: "var(--pw-status-success)",
                                        fontWeight: 700,
                                    }}
                                >
                                    40% tổng điểm
                                </span>
                            </div>
                            <p
                                style={{
                                    margin: 0,
                                    fontSize: "0.8rem",
                                    color: "var(--pw-text-secondary)",
                                }}
                            >
                                So sánh biểu phí theo thời gian dự kiến đỗ, ưu
                                tiên các bãi có mức giá hợp lý và tiết kiệm chi
                                phí.
                            </p>
                        </div>

                        {/* 20% Availability Probability */}
                        <div
                            style={{
                                padding: "var(--pw-space-sm)",
                                backgroundColor: "var(--pw-surface-subtle)",
                                borderRadius: "var(--pw-radius-md)",
                                border: "1px solid var(--pw-border-subtle)",
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                    marginBottom: "4px",
                                }}
                            >
                                <span
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: "6px",
                                        fontWeight: 700,
                                        color: "var(--pw-text-primary)",
                                    }}
                                >
                                    <CheckCircle2
                                        size={16}
                                        color="var(--pw-status-info)"
                                    />
                                    Khả năng còn chỗ lúc tới
                                </span>
                                <span
                                    className="ai-panel-badge"
                                    style={{
                                        backgroundColor:
                                            "var(--pw-status-info-bg)",
                                        color: "var(--pw-status-info)",
                                        fontWeight: 700,
                                    }}
                                >
                                    20% tổng điểm
                                </span>
                            </div>
                            <p
                                style={{
                                    margin: 0,
                                    fontSize: "0.8rem",
                                    color: "var(--pw-text-secondary)",
                                }}
                            >
                                Dự báo xác suất bãi đỗ còn chỗ trống đúng thời
                                điểm bạn tới bãi, dựa trên lịch sử dữ liệu và
                                thuật toán AI.
                            </p>
                        </div>
                    </div>

                    {/* Guardrails */}
                    <div
                        style={{
                            padding: "var(--pw-space-sm)",
                            backgroundColor: "var(--pw-status-warning-bg)",
                            border: "1px solid var(--pw-status-warning-border)",
                            borderRadius: "var(--pw-radius-md)",
                            display: "flex",
                            flexDirection: "column",
                            gap: "6px",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: "6px",
                                fontWeight: 700,
                                fontSize: "0.85rem",
                                color: "var(--pw-status-warning)",
                            }}
                        >
                            <AlertTriangle size={16} />
                            Quy tắc an toàn chống rủi ro hết chỗ
                        </div>
                        <ul
                            style={{
                                margin: 0,
                                paddingLeft: "18px",
                                fontSize: "0.75rem",
                                color: "var(--pw-text-secondary)",
                                lineHeight: 1.5,
                            }}
                        >
                            <li>
                                <strong>Phạt điểm rủi ro:</strong> Bãi xe có xác
                                suất còn chỗ dưới 30% bị phạt 30 điểm và gắn
                                nhãn cảnh báo nguy cơ hết chỗ.
                            </li>
                            <li>
                                <strong>Bảo vệ Hạng 1:</strong> Bãi xe có xác
                                suất còn chỗ dưới 30% tuyệt đối không được xếp
                                Hạng 1 nếu có bất kỳ bãi xe an toàn nào khác
                                xung quanh.
                            </li>
                            <li>
                                <strong>Chế độ dự phòng:</strong> Nếu không tính
                                được thời gian lái xe, hệ thống tự động phân bổ
                                67% xác suất còn chỗ và 33% giá đỗ.
                            </li>
                        </ul>
                    </div>
                </div>

                <div className="ai-modal-footer">
                    <button
                        type="button"
                        className="ai-modal-ok-btn"
                        onClick={onClose}
                    >
                        Đã hiểu
                    </button>
                </div>
            </div>
        </div>
    );
}
