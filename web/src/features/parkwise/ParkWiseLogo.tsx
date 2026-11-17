import Image from "next/image";
import { parkwiseAssets } from "./parkwiseAssets";

/** Canonical ParkWise asset for the shared header and footer. */
export function ParkWiseLogo({ priority = false }: { priority?: boolean }) {
    return (
        <Image
            src={parkwiseAssets.logo}
            alt="ParkWise"
            width={707}
            height={353}
            priority={priority}
        />
    );
}
