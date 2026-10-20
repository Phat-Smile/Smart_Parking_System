'use client';

import React, { useState, useRef, useEffect, useMemo } from 'react';
import Link from 'next/link';
import { useAllLots, useOwnerMapStore } from './useOwnerMapStore';
import { getLotCoordinates } from './lotGeo';
import { useLotInfo, formatVnd, formatDateVi } from './lotInfo';

interface GoogleStyleMapProps {
  filteredLots: ReturnType<typeof useAllLots>;
}

// Google Maps Tile Subdomains
const GOOGLE_TILE_SERVERS = ['mt0', 'mt1', 'mt2', 'mt3'];

// Latitude offset when focusing a lot so pin is lower on screen and popup doesn't clip
const FOCUS_LAT_OFFSET = 0.0036;

// Web Mercator projection formulas (EPSG:3857)
function latLonToPixel(lat: number, lon: number, zoom: number): { x: number; y: number } {
  const siny = Math.sin((lat * Math.PI) / 180);
  const clampedSiny = Math.min(Math.max(siny, -0.9999), 0.9999);
  const scale = 256 * Math.pow(2, zoom);
  return {
    x: (0.5 + lon / 360) * scale,
    y: (0.5 - Math.log((1 + clampedSiny) / (1 - clampedSiny)) / (4 * Math.PI)) * scale,
  };
}

function pixelToLatLon(x: number, y: number, zoom: number): { lat: number; lon: number } {
  const scale = 256 * Math.pow(2, zoom);
  const lon = (x / scale - 0.5) * 360;
  const y2 = 0.5 - y / scale;
  const lat = 90 - (360 * Math.atan(Math.exp(-y2 * 2 * Math.PI))) / Math.PI;
  return { lat, lon };
}

function getMetersPerPixel(lat: number, zoom: number): number {
  return (156543.03392 * Math.cos((lat * Math.PI) / 180)) / Math.pow(2, zoom);
}

export function GoogleStyleMap({ filteredLots }: GoogleStyleMapProps) {
  const { selectedLotId, setSelectedLotId } = useOwnerMapStore();
  const selectedLot = filteredLots.find((l) => l.id === selectedLotId);
  const selectedLotInfo = useLotInfo(selectedLotId);

  // Map viewport state: center lat/lon and zoom
  const [center, setCenter] = useState<{ lat: number; lon: number }>(() => {
    if (selectedLotId) {
      const c = getLotCoordinates(selectedLotId);
      return { lon: c[0], lat: c[1] + FOCUS_LAT_OFFSET };
    }
    return { lon: 106.7035, lat: 10.7745 + FOCUS_LAT_OFFSET }; // Nguyễn Huệ, Quận 1
  });

  const [zoom, setZoom] = useState<number>(14.5);
  const [mapType, setMapType] = useState<'streets' | 'satellite'>('streets');
  const [isFullscreen, setIsFullscreen] = useState(false);

  // Container dimensions
  const containerRef = useRef<HTMLDivElement>(null);
  const [dimensions, setDimensions] = useState({ width: 800, height: 440 });

  // Drag interaction state
  const isDraggingRef = useRef(false);
  const dragStartRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });
  const centerStartRef = useRef<{ lat: number; lon: number }>({ lat: 0, lon: 0 });
  const [isCursorGrabbing, setIsCursorGrabbing] = useState(false);

  // Measure container
  useEffect(() => {
    const updateSize = () => {
      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect();
        setDimensions({
          width: Math.max(300, Math.round(rect.width)),
          height: Math.max(260, Math.round(rect.height)),
        });
      }
    };
    updateSize();
    window.addEventListener('resize', updateSize);
    return () => window.removeEventListener('resize', updateSize);
  }, [isFullscreen]);

  // When selectedLotId changes from outside, pan to it with comfortable vertical offset
  const [prevLotId, setPrevLotId] = useState(selectedLotId);
  if (selectedLotId !== prevLotId) {
    setPrevLotId(selectedLotId);
    if (selectedLotId) {
      const coords = getLotCoordinates(selectedLotId);
      setCenter({ lon: coords[0], lat: coords[1] + FOCUS_LAT_OFFSET });
      setZoom((prev) => Math.max(14.5, prev));
    }
  }

  // Pan handlers
  const handlePointerDown = (e: React.PointerEvent) => {
    if (e.button !== 0 && e.pointerType === 'mouse') return;
    isDraggingRef.current = true;
    dragStartRef.current = { x: e.clientX, y: e.clientY };
    centerStartRef.current = { ...center };
    setIsCursorGrabbing(true);
    (e.target as HTMLElement).setPointerCapture?.(e.pointerId);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    if (!isDraggingRef.current) return;
    const dx = e.clientX - dragStartRef.current.x;
    const dy = e.clientY - dragStartRef.current.y;

    const startPixel = latLonToPixel(
      centerStartRef.current.lat,
      centerStartRef.current.lon,
      zoom
    );
    const newPixel = {
      x: startPixel.x - dx,
      y: startPixel.y - dy,
    };
    const newCenter = pixelToLatLon(newPixel.x, newPixel.y, zoom);
    setCenter(newCenter);
  };

  const handlePointerUp = (e: React.PointerEvent) => {
    if (isDraggingRef.current) {
      isDraggingRef.current = false;
      setIsCursorGrabbing(false);
      try {
        (e.target as HTMLElement).releasePointerCapture?.(e.pointerId);
      } catch {
        // ignore
      }
    }
  };

  // Mouse wheel zoom centered on cursor
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    const zoomDelta = e.deltaY > 0 ? -0.35 : 0.35;
    setZoom((z) => Math.min(18, Math.max(11, Math.round((z + zoomDelta) * 10) / 10)));
  };

  // Double click to zoom in
  const handleDoubleClick = (e: React.MouseEvent) => {
    if (!containerRef.current) return;
    const rect = containerRef.current.getBoundingClientRect();
    const clickX = e.clientX - rect.left - dimensions.width / 2;
    const clickY = e.clientY - rect.top - dimensions.height / 2;

    const centerPixel = latLonToPixel(center.lat, center.lon, zoom);
    const targetPixel = {
      x: centerPixel.x + clickX,
      y: centerPixel.y + clickY,
    };
    const targetCoords = pixelToLatLon(targetPixel.x, targetPixel.y, zoom);
    setCenter(targetCoords);
    setZoom((z) => Math.min(18, Math.floor(z + 1)));
  };

  // Zoom controls
  const handleZoomIn = () => setZoom((z) => Math.min(18, Math.round((z + 1) * 2) / 2));
  const handleZoomOut = () => setZoom((z) => Math.max(11, Math.round((z - 1) * 2) / 2));
  const handleRecenter = () => {
    if (selectedLotId) {
      const coords = getLotCoordinates(selectedLotId);
      setCenter({ lon: coords[0], lat: coords[1] + FOCUS_LAT_OFFSET });
      setZoom(15);
    } else {
      setCenter({ lon: 106.7035, lat: 10.7745 + FOCUS_LAT_OFFSET });
      setZoom(14.5);
    }
  };

  // Visible tiles calculation with DIRECT screen coordinates
  const intZoom = Math.floor(zoom);
  const zoomScale = Math.pow(2, zoom - intZoom);
  const centerPixelInt = latLonToPixel(center.lat, center.lon, intZoom);
  const tileSize = 256 * zoomScale;

  const halfW = dimensions.width / (2 * zoomScale);
  const halfH = dimensions.height / (2 * zoomScale);

  const minX = Math.floor((centerPixelInt.x - halfW) / 256) - 1;
  const maxX = Math.floor((centerPixelInt.x + halfW) / 256) + 1;
  const minY = Math.floor((centerPixelInt.y - halfH) / 256) - 1;
  const maxY = Math.floor((centerPixelInt.y + halfH) / 256) + 1;

  const maxTileIndex = Math.pow(2, intZoom) - 1;

  interface TileItem {
    key: string;
    screenX: number;
    screenY: number;
    size: number;
    url: string;
  }

  const visibleTiles: TileItem[] = [];
  for (let tx = minX; tx <= maxX; tx++) {
    for (let ty = minY; ty <= maxY; ty++) {
      if (ty < 0 || ty > maxTileIndex) continue;
      const wrappedX = ((tx % (maxTileIndex + 1)) + (maxTileIndex + 1)) % (maxTileIndex + 1);

      // Direct screen coordinates relative to container top-left
      const screenX = dimensions.width / 2 + (tx * 256 - centerPixelInt.x) * zoomScale;
      const screenY = dimensions.height / 2 + (ty * 256 - centerPixelInt.y) * zoomScale;

      const sub = GOOGLE_TILE_SERVERS[Math.abs(tx + ty) % GOOGLE_TILE_SERVERS.length];

      // Google Maps Tile API (lyrs=m for standard vector streets, lyrs=y for hybrid satellite with road labels)
      const url =
        mapType === 'streets'
          ? `https://${sub}.google.com/vt/lyrs=m&hl=vi&x=${wrappedX}&y=${ty}&z=${intZoom}`
          : `https://${sub}.google.com/vt/lyrs=y&hl=vi&x=${wrappedX}&y=${ty}&z=${intZoom}`;

      visibleTiles.push({
        key: `gm-${mapType}-${intZoom}-${wrappedX}-${ty}`,
        screenX,
        screenY,
        size: tileSize,
        url,
      });
    }
  }

  // Scale bar calculation
  const metersPerPixel = getMetersPerPixel(center.lat, zoom);
  const targetBarWidth = 100;
  const rawMeters = metersPerPixel * targetBarWidth;
  let niceMeters = 100;
  if (rawMeters > 5000) niceMeters = 5000;
  else if (rawMeters > 2000) niceMeters = 2000;
  else if (rawMeters > 1000) niceMeters = 1000;
  else if (rawMeters > 500) niceMeters = 500;
  else if (rawMeters > 200) niceMeters = 200;
  else if (rawMeters > 100) niceMeters = 100;
  else niceMeters = 50;
  const scaleBarWidthPx = Math.round(niceMeters / metersPerPixel);
  const scaleBarText = niceMeters >= 1000 ? `${niceMeters / 1000} km` : `${niceMeters} m`;

  // Status info for selected lot popup
  const statusBadge = useMemo(() => {
    if (!selectedLot) return null;
    if (selectedLot.ap === 'approved') {
      return { bg: '#def7ec', text: '#0e9f6e', label: 'Đang hoạt động' };
    }
    if (selectedLot.ap === 'pending') {
      return { bg: '#fef3c7', text: '#b45309', label: 'Chờ duyệt' };
    }
    return { bg: '#fde8e8', text: '#e5484d', label: 'Bị từ chối' };
  }, [selectedLot]);

  return (
    <div
      ref={containerRef}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onWheel={handleWheel}
      onDoubleClick={handleDoubleClick}
      style={{
        position: isFullscreen ? 'fixed' : 'relative',
        inset: isFullscreen ? 0 : undefined,
        zIndex: isFullscreen ? 9999 : 1,
        width: '100%',
        height: isFullscreen ? '100vh' : '100%',
        overflow: 'hidden',
        background: mapType === 'streets' ? '#eef0f2' : '#0a192f',
        cursor: isCursorGrabbing ? 'grabbing' : 'grab',
        userSelect: 'none',
        touchAction: 'none',
        fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
      }}
    >
      {/* Real-time Google Maps Tile Layer */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          pointerEvents: 'none',
          overflow: 'hidden',
        }}
      >
        {visibleTiles.map((tile) => (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            key={tile.key}
            src={tile.url}
            alt=""
            loading="eager"
            decoding="async"
            draggable={false}
            style={{
              position: 'absolute',
              left: Math.round(tile.screenX),
              top: Math.round(tile.screenY),
              width: Math.ceil(tile.size) + 1,
              height: Math.ceil(tile.size) + 1,
              display: 'block',
              userSelect: 'none',
              pointerEvents: 'none',
            }}
          />
        ))}
      </div>

      {/* Pins Layer (Anchored to exact real-world Lat/Lon on Google Maps grid) */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          pointerEvents: 'none',
          zIndex: 20,
        }}
      >
        {filteredLots.map((lot) => {
          const coords = getLotCoordinates(lot.id);
          const pinPixel = latLonToPixel(coords[1], coords[0], zoom);
          const centerPixel = latLonToPixel(center.lat, center.lon, zoom);
          const screenX = dimensions.width / 2 + (pinPixel.x - centerPixel.x);
          const screenY = dimensions.height / 2 + (pinPixel.y - centerPixel.y);
          const isSelected = selectedLotId === lot.id;

          const pinColor =
            lot.ap === 'approved' ? '#0f9d58' : lot.ap === 'pending' ? '#f4b400' : '#ea4335';

          return (
            <div
              key={lot.id}
              onClick={(e) => {
                e.stopPropagation();
                setSelectedLotId(isSelected ? null : lot.id);
              }}
              style={{
                position: 'absolute',
                left: Math.round(screenX),
                top: Math.round(screenY),
                transform: `translate(-50%, -100%) scale(${isSelected ? 1.15 : 1})`,
                transformOrigin: '50% 100%',
                pointerEvents: 'auto',
                cursor: 'pointer',
                zIndex: isSelected ? 40 : 25,
                transition: 'transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1)',
              }}
            >
              {/* Google Maps Pin Teardrop Marker */}
              <div
                style={{
                  position: 'relative',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                }}
              >
                <svg
                  width={isSelected ? 36 : 30}
                  height={isSelected ? 48 : 40}
                  viewBox="0 0 30 40"
                  fill="none"
                  style={{
                    filter: isSelected
                      ? 'drop-shadow(0 4px 8px rgba(0,0,0,0.35))'
                      : 'drop-shadow(0 2px 5px rgba(0,0,0,0.25))',
                  }}
                >
                  <path
                    d="M15 0C6.71573 0 0 6.71573 0 15C0 24 15 40 15 40C15 40 30 24 30 15C30 6.71573 23.2843 0 15 0Z"
                    fill={pinColor}
                  />
                  <circle cx="15" cy="14" r="9" fill="#ffffff" />
                  <text
                    x="15"
                    y="18"
                    textAnchor="middle"
                    fill={pinColor}
                    fontSize="11.5"
                    fontWeight="800"
                    fontFamily="'Inter', -apple-system, sans-serif"
                  >
                    P
                  </text>
                </svg>

                {/* Google Maps POI Label Pill */}
                <div
                  style={{
                    marginTop: 2,
                    background: 'rgba(255, 255, 255, 0.95)',
                    padding: '2px 8px',
                    borderRadius: 10,
                    boxShadow: '0 1px 4px rgba(0,0,0,0.2)',
                    fontSize: 11,
                    fontWeight: 700,
                    color: '#202124',
                    whiteSpace: 'nowrap',
                    border: isSelected ? `1.5px solid ${pinColor}` : '1px solid rgba(0,0,0,0.08)',
                  }}
                >
                  {lot.name}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Google Maps InfoWindow Popup */}
      {selectedLot && selectedLotInfo && statusBadge && (
        (() => {
          const coords = getLotCoordinates(selectedLot.id);
          const pinPixel = latLonToPixel(coords[1], coords[0], zoom);
          const centerPixel = latLonToPixel(center.lat, center.lon, zoom);
          const pinScreenX = dimensions.width / 2 + (pinPixel.x - centerPixel.x);
          const pinScreenY = dimensions.height / 2 + (pinPixel.y - centerPixel.y);

          // If pin is near the top edge (< 190px), anchor popup below the pin
          const anchorBelow = pinScreenY < 190;
          const popupX = Math.max(145, Math.min(dimensions.width - 145, pinScreenX));
          const popupY = anchorBelow ? pinScreenY + 12 : pinScreenY - 48;

          return (
            <div
              onClick={(e) => e.stopPropagation()}
              role="dialog"
              aria-label={`Thông tin nhanh bãi ${selectedLot.name}`}
              style={{
                position: 'absolute',
                left: popupX,
                top: popupY,
                transform: anchorBelow ? 'translate(-50%, 0)' : 'translate(-50%, -100%)',
                zIndex: 60,
                width: 275,
                background: '#ffffff',
                borderRadius: 14,
                boxShadow: '0 8px 24px rgba(0,0,0,0.22), 0 2px 6px rgba(0,0,0,0.12)',
                padding: '13px 15px',
                border: '1px solid #e8eaed',
                pointerEvents: 'auto',
                animation: 'ov2-fade-in 0.18s ease-out',
              }}
            >
              {/* Header with Title and Close Button */}
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'flex-start',
                  gap: 8,
                }}
              >
                <div>
                  <div style={{ fontSize: 14, fontWeight: 800, color: '#202124' }}>
                    {selectedLot.name}
                  </div>
                  <div style={{ fontSize: 11, color: '#5f6368', marginTop: 2 }}>
                    📍 {selectedLot.addr}
                  </div>
                </div>

                <button
                  type="button"
                  onClick={() => setSelectedLotId(null)}
                  aria-label="Đóng popup"
                  style={{
                    background: '#f1f3f4',
                    border: 'none',
                    borderRadius: '50%',
                    width: 22,
                    height: 22,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: 'pointer',
                    fontSize: 12,
                    color: '#5f6368',
                    lineHeight: 1,
                  }}
                >
                  ✕
                </button>
              </div>

              {/* Status Badge */}
              <div style={{ marginTop: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
                <span
                  style={{
                    display: 'inline-block',
                    padding: '2px 8px',
                    borderRadius: 999,
                    fontSize: 11,
                    fontWeight: 700,
                    background: statusBadge.bg,
                    color: statusBadge.text,
                  }}
                >
                  ● {statusBadge.label}
                </span>

                <span style={{ fontSize: 11, color: '#70757a' }}>
                  {selectedLot.ap === 'approved' && selectedLotInfo.openedAt
                    ? `Từ ${formatDateVi(selectedLotInfo.openedAt)}`
                    : selectedLot.ap === 'pending'
                    ? 'Chờ PCCC'
                    : 'Cần bổ sung'}
                </span>
              </div>

              {/* Quick Metrics */}
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr 1fr',
                  gap: 8,
                  marginTop: 10,
                  padding: '7px 9px',
                  background: '#f8f9fa',
                  borderRadius: 8,
                  fontSize: 11,
                }}
              >
                <div>
                  <div style={{ color: '#70757a', fontSize: 10 }}>SỨC CHỨA</div>
                  <div style={{ fontWeight: 700, color: '#202124', marginTop: 1 }}>
                    {selectedLot.cc} ô tô
                  </div>
                </div>
                <div>
                  <div style={{ color: '#70757a', fontSize: 10 }}>GIỜ MỞ CỬA</div>
                  <div style={{ fontWeight: 700, color: '#202124', marginTop: 1 }}>
                    {selectedLotInfo.openingHours || '24/7'}
                  </div>
                </div>
              </div>

              {/* Price Snippet */}
              <div
                style={{
                  marginTop: 8,
                  fontSize: 11.5,
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <span style={{ color: '#5f6368' }}>Giá từ:</span>
                <span style={{ fontWeight: 800, color: '#1a73e8' }}>
                  {formatVnd(selectedLotInfo.prices.carFirstHour || selectedLot.pc)} / lượt
                </span>
              </div>

              {/* Action Buttons */}
              <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                <Link
                  href="/owner/layout/3d"
                  style={{
                    flex: 1,
                    textAlign: 'center',
                    background: '#1a73e8',
                    color: '#ffffff',
                    padding: '6px 10px',
                    borderRadius: 8,
                    fontSize: 11.5,
                    fontWeight: 700,
                    textDecoration: 'none',
                    display: 'block',
                  }}
                >
                  Xem sơ đồ 3D
                </Link>
              </div>

              {/* Triangle Beak Pointer */}
              <div
                style={{
                  position: 'absolute',
                  [anchorBelow ? 'top' : 'bottom']: -8,
                  left: '50%',
                  transform: 'translateX(-50%)',
                  width: 0,
                  height: 0,
                  borderLeft: '8px solid transparent',
                  borderRight: '8px solid transparent',
                  [anchorBelow ? 'borderBottom' : 'borderTop']: '8px solid #ffffff',
                  filter: 'drop-shadow(0 2px 2px rgba(0,0,0,0.12))',
                }}
              />
            </div>
          );
        })()
      )}

      {/* Google Maps Top-Right Map/Satellite Switcher */}
      <div
        style={{
          position: 'absolute',
          top: 14,
          right: 14,
          zIndex: 30,
          display: 'flex',
          background: '#ffffff',
          borderRadius: 8,
          boxShadow: '0 2px 6px rgba(0,0,0,0.3)',
          overflow: 'hidden',
          border: '1px solid #dadce0',
        }}
      >
        <button
          type="button"
          onClick={() => setMapType('streets')}
          style={{
            padding: '7px 14px',
            fontSize: 12,
            fontWeight: 700,
            border: 'none',
            background: mapType === 'streets' ? '#ffffff' : '#f8f9fa',
            color: mapType === 'streets' ? '#1a73e8' : '#5f6368',
            cursor: 'pointer',
            borderRight: '1px solid #dadce0',
            transition: 'background 0.15s, color 0.15s',
          }}
        >
          Bản đồ
        </button>
        <button
          type="button"
          onClick={() => setMapType('satellite')}
          style={{
            padding: '7px 14px',
            fontSize: 12,
            fontWeight: 700,
            border: 'none',
            background: mapType === 'satellite' ? '#ffffff' : '#f8f9fa',
            color: mapType === 'satellite' ? '#1a73e8' : '#5f6368',
            cursor: 'pointer',
            transition: 'background 0.15s, color 0.15s',
          }}
        >
          Vệ tinh
        </button>
      </div>

      {/* Google Maps Bottom-Right Zoom & Recenter Controls */}
      <div
        style={{
          position: 'absolute',
          bottom: 24,
          right: 14,
          zIndex: 30,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 10,
        }}
      >
        {/* Recenter Crosshair Button */}
        <button
          type="button"
          onClick={handleRecenter}
          title="Đưa về vị trí bãi đỗ xe"
          aria-label="Đưa về vị trí bãi đỗ xe"
          style={{
            width: 36,
            height: 36,
            borderRadius: 8,
            background: '#ffffff',
            boxShadow: '0 2px 6px rgba(0,0,0,0.3)',
            border: '1px solid #dadce0',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            color: '#5f6368',
            fontSize: 18,
          }}
        >
          ⌖
        </button>

        {/* Zoom In & Out Stack */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            background: '#ffffff',
            borderRadius: 8,
            boxShadow: '0 2px 6px rgba(0,0,0,0.3)',
            border: '1px solid #dadce0',
            overflow: 'hidden',
          }}
        >
          <button
            type="button"
            onClick={handleZoomIn}
            title="Phóng to"
            aria-label="Phóng to bản đồ"
            style={{
              width: 36,
              height: 36,
              border: 'none',
              background: '#ffffff',
              fontSize: 20,
              fontWeight: 500,
              color: '#5f6368',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              borderBottom: '1px solid #dadce0',
            }}
          >
            +
          </button>
          <button
            type="button"
            onClick={handleZoomOut}
            title="Thu nhỏ"
            aria-label="Thu nhỏ bản đồ"
            style={{
              width: 36,
              height: 36,
              border: 'none',
              background: '#ffffff',
              fontSize: 22,
              fontWeight: 500,
              color: '#5f6368',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            −
          </button>
        </div>

        {/* Fullscreen Button */}
        <button
          type="button"
          onClick={() => setIsFullscreen(!isFullscreen)}
          title={isFullscreen ? 'Thu nhỏ cửa sổ' : 'Toàn màn hình'}
          aria-label="Chế độ toàn màn hình"
          style={{
            width: 36,
            height: 36,
            borderRadius: 8,
            background: '#ffffff',
            boxShadow: '0 2px 6px rgba(0,0,0,0.3)',
            border: '1px solid #dadce0',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            color: '#5f6368',
            fontSize: 15,
          }}
        >
          {isFullscreen ? '✕' : '⛶'}
        </button>
      </div>

      {/* Google Maps Bottom-Left Scale Bar & Attribution */}
      <div
        style={{
          position: 'absolute',
          bottom: 10,
          left: 14,
          zIndex: 30,
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          pointerEvents: 'none',
        }}
      >
        {/* Scale Bar */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
          }}
        >
          <div
            style={{
              fontSize: 10,
              fontWeight: 700,
              color: mapType === 'streets' ? '#202124' : '#ffffff',
              textShadow: '0 1px 2px rgba(0,0,0,0.6)',
              marginBottom: 1,
            }}
          >
            {scaleBarText}
          </div>
          <div
            style={{
              width: scaleBarWidthPx,
              height: 4,
              borderBottom: `2px solid ${mapType === 'streets' ? '#202124' : '#ffffff'}`,
              borderLeft: `2px solid ${mapType === 'streets' ? '#202124' : '#ffffff'}`,
              borderRight: `2px solid ${mapType === 'streets' ? '#202124' : '#ffffff'}`,
              filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.6))',
            }}
          />
        </div>

        {/* Google Maps Logo & Attribution */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            fontSize: 10,
            color: mapType === 'streets' ? 'rgba(32,33,36,0.75)' : 'rgba(255,255,255,0.85)',
            textShadow: '0 1px 2px rgba(0,0,0,0.5)',
          }}
        >
          <span style={{ fontWeight: 800, letterSpacing: -0.2 }}>
            <span style={{ color: '#4285f4' }}>G</span>
            <span style={{ color: '#ea4335' }}>o</span>
            <span style={{ color: '#fbbc05' }}>o</span>
            <span style={{ color: '#4285f4' }}>g</span>
            <span style={{ color: '#34a853' }}>l</span>
            <span style={{ color: '#ea4335' }}>e</span>
          </span>
          <span>Maps</span>
        </div>
      </div>
    </div>
  );
}
