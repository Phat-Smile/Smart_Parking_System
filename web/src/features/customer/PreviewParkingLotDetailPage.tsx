import { ParkingLotDetailExperience } from "./ParkingLotDetailExperience";
import { previewParkingLots } from "./previewParkingLots";

export function PreviewParkingLotDetailPage({
    lot,
    fontClassName,
}: {
    lot: (typeof previewParkingLots)[number];
    fontClassName: string;
}) {
    const image =
        lot.id === "parkside"
            ? "/images/parkwise/detail-parkside-hero.png"
            : lot.image;

    return (
        <ParkingLotDetailExperience
            fontClassName={fontClassName}
            lot={{
                id: lot.id,
                name: lot.name,
                address: lot.address,
                type: lot.type,
                image,
                photos: lot.id === "parkside" ? [image, lot.image] : [image],
                isPreview: true,
                hourlyPrice: lot.hourlyPrice,
                distanceKm: lot.distanceKm,
                travelMinutes: lot.travelMinutes,
                rating: lot.rating,
                reviewCount: lot.reviewCount,
                spaces: lot.spaces,
                compatible: lot.compatible,
                amenities: lot.amenities,
            }}
        />
    );
}
