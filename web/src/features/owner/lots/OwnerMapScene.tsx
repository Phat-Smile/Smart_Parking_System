'use client';

import React from 'react';
import Map, { Marker, NavigationControl } from 'react-map-gl/mapbox';
import { useAllLots } from './useOwnerMapStore';
import { getLotCoordinates } from './lotGeo';
import { LotPin } from './LotPin';

interface OwnerMapSceneProps {
  token: string;
  selectedLotId: number | null;
  onSelectLot: (id: number) => void;
  lots: ReturnType<typeof useAllLots>;
}

export default function OwnerMapScene({
  token,
  selectedLotId,
  onSelectLot,
  lots,
}: OwnerMapSceneProps) {
  const activeCoords = selectedLotId
    ? getLotCoordinates(selectedLotId)
    : [106.7050, 10.7600];

  return (
    <Map
      key={selectedLotId ? `lot-${selectedLotId}` : 'default-map'}
      initialViewState={{
        longitude: activeCoords[0],
        latitude: activeCoords[1],
        zoom: selectedLotId ? 14 : 12,
      }}
      mapboxAccessToken={token}
      style={{ width: '100%', height: '100%' }}
      mapStyle="mapbox://styles/mapbox/light-v11"
      aria-label="Bản đồ vị trí các bãi đỗ xe"
    >
      <NavigationControl position="bottom-right" />

      {lots.map((lot) => {
        const coords = getLotCoordinates(lot.id);
        return (
          <Marker
            key={lot.id}
            longitude={coords[0]}
            latitude={coords[1]}
            anchor="center"
          >
            <LotPin
              lot={lot}
              isSelected={selectedLotId === lot.id}
              onClick={() => onSelectLot(lot.id)}
            />
          </Marker>
        );
      })}
    </Map>
  );
}
