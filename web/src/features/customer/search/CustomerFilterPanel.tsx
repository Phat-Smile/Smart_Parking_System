"use client";

export type CustomerFilterPanelProps = {
    distanceChoice: string;
    customDistanceInput: string;
    setCustomDistanceInput: (val: string) => void;
    customDistanceError: string;
    setCustomDistanceError: (msg: string) => void;
    applyCustomDistance: () => void;
    copy: (en: string, vi: string) => string;
};

export function CustomerFilterPanel({
    distanceChoice,
    customDistanceInput,
    setCustomDistanceInput,
    customDistanceError,
    setCustomDistanceError,
    applyCustomDistance,
    copy,
}: CustomerFilterPanelProps) {
    if (distanceChoice !== "custom") return null;

    return (
        <div className="customer-search-custom-distance">
            <label htmlFor="custom-distance-km">
                {copy("Distance in km", "Khoảng cách (km)")}
            </label>
            <div className="customer-search-custom-distance-row">
                <input
                    id="custom-distance-km"
                    type="number"
                    min="0.1"
                    max="50"
                    step="0.1"
                    inputMode="decimal"
                    required
                    value={customDistanceInput}
                    onChange={(event) => {
                        setCustomDistanceInput(event.target.value);
                        setCustomDistanceError("");
                    }}
                    aria-invalid={Boolean(customDistanceError)}
                    aria-describedby={
                        customDistanceError
                            ? "custom-distance-error"
                            : "custom-distance-hint"
                    }
                />
                <button
                    type="button"
                    className="customer-search-apply-distance"
                    onClick={applyCustomDistance}
                >
                    {copy("Apply", "Áp dụng")}
                </button>
            </div>
            <small
                id={
                    customDistanceError
                        ? "custom-distance-error"
                        : "custom-distance-hint"
                }
                role={customDistanceError ? "alert" : undefined}
            >
                {customDistanceError ||
                    copy(
                        "Enter a value from 0.1 to 50 km.",
                        "Nhập giá trị từ 0,1 đến 50 km.",
                    )}
            </small>
        </div>
    );
}
