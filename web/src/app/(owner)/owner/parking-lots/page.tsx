import React from 'react';
import { ParkingLotsClientView } from '@/features/owner/lots/ParkingLotsClientView';

export default function ParkingLotsPage() {
  // Server-side reading of MAPBOX_ACCESS_TOKEN or NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN
  const mapboxToken =
    process.env.MAPBOX_ACCESS_TOKEN?.trim() ||
    process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN?.trim() ||
    '';

  return <ParkingLotsClientView mapboxToken={mapboxToken} />;
}
