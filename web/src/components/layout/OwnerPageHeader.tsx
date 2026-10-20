'use client';

import React, { useState, useRef, useEffect, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useOwnerStore } from '@/store/useOwnerStore';
import { useNotificationStore } from '@/features/owner/notifications/useNotificationStore';
import { NotificationCategory } from '@/features/owner/notifications/notificationTypes';

export interface OwnerPageHeaderProps {
  breadcrumb?: string;
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
}

const CATEGORY_TABS: { key: NotificationCategory; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'lots', label: 'Bãi của tôi' },
  { key: 'reservations', label: 'Đặt chỗ' },
  { key: 'sessions', label: 'Phiên đỗ xe' },
  { key: 'layout', label: 'Sơ đồ' },
  { key: 'reports', label: 'Báo cáo' },
  { key: 'system', label: 'Hệ thống' },
];

export function OwnerPageHeader({
  breadcrumb,
  title,
  subtitle,
  actions,
}: OwnerPageHeaderProps) {
  const router = useRouter();
  const { lots, reservations, spots } = useOwnerStore();
  const {
    notifications,
    activeTab,
    setActiveTab,
    markAsRead,
    markAllAsRead,
  } = useNotificationStore();

  const [searchQuery, setSearchQuery] = useState('');
  const [searchOpen, setSearchOpen] = useState(false);
  const [notifOpen, setNotifOpen] = useState(false);

  const searchRef = useRef<HTMLDivElement>(null);
  const notifRef = useRef<HTMLDivElement>(null);

  const unreadCount = useMemo(
    () => notifications.filter((n) => !n.read).length,
    [notifications]
  );

  // Close popups on click outside or Escape
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (searchRef.current && !searchRef.current.contains(e.target as Node)) {
        setSearchOpen(false);
      }
      if (notifRef.current && !notifRef.current.contains(e.target as Node)) {
        setNotifOpen(false);
      }
    }

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        setSearchOpen(false);
        setNotifOpen(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  // Global search filtering
  const searchResults = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    if (!q) return null;

    const matchedLots = lots.filter(
      (l) =>
        l.name.toLowerCase().includes(q) ||
        (l.addr && l.addr.toLowerCase().includes(q))
    );

    const matchedReservations = reservations.filter(
      (r) =>
        r.id.toLowerCase().includes(q) ||
        r.plate.toLowerCase().includes(q) ||
        r.slot.toLowerCase().includes(q)
    );

    // From active sessions/spots
    const matchedSpots = spots.filter(
      (s) => (s.p && s.p.toLowerCase().includes(q)) || s.id.toLowerCase().includes(q)
    );

    return {
      lots: matchedLots.slice(0, 3),
      reservations: matchedReservations.slice(0, 3),
      spots: matchedSpots.slice(0, 3),
      total: matchedLots.length + matchedReservations.length + matchedSpots.length,
    };
  }, [searchQuery, lots, reservations, spots]);

  const filteredNotifications = useMemo(() => {
    if (activeTab === 'all') return notifications;
    return notifications.filter((n) => n.category === activeTab);
  }, [notifications, activeTab]);

  return (
    <header className="ov2-header" style={{ position: 'relative', marginBottom: 24, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16 }}>
      <div className="ov2-header-left">
        {breadcrumb && (
          <div className="ov2-breadcrumb" style={{ fontSize: 13, color: '#6a7491', marginBottom: 4 }}>
            {breadcrumb}
          </div>
        )}
        <h1 style={{ fontSize: 26, fontWeight: 800, margin: 0, color: '#0f1b3d' }}>
          {title}
        </h1>
        {subtitle && (
          <div style={{ fontSize: 13.5, color: '#6a7491', marginTop: 4 }}>
            {subtitle}
          </div>
        )}
      </div>

      <div className="ov2-header-right" style={{ display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
        {actions && <div className="ov2-header-actions">{actions}</div>}

        {/* Global Search */}
        <div ref={searchRef} style={{ position: 'relative' }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              background: '#f3f6fb',
              borderRadius: 999,
              padding: '8px 16px',
              width: 280,
              border: '1px solid #e3e8f2',
            }}
          >
            <svg
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="#6a7491"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              style={{ marginRight: 8, flexShrink: 0 }}
            >
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              type="text"
              placeholder="Tìm biển số, bãi, mã đặt chỗ"
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setSearchOpen(true);
              }}
              onFocus={() => setSearchOpen(true)}
              aria-label="Tìm kiếm toàn cục"
              style={{
                border: 'none',
                background: 'transparent',
                outline: 'none',
                width: '100%',
                fontSize: 13,
                color: '#0f1b3d',
              }}
            />
          </div>

          {/* Search Dropdown Results */}
          {searchOpen && searchQuery.trim() !== '' && (
            <div
              style={{
                position: 'absolute',
                top: 'calc(100% + 8px)',
                right: 0,
                width: 360,
                background: '#fff',
                borderRadius: 14,
                boxShadow: '0 10px 25px rgba(15,27,61,0.12)',
                border: '1px solid #e3e8f2',
                zIndex: 100,
                padding: '12px 0',
                maxHeight: 380,
                overflowY: 'auto',
              }}
            >
              {searchResults && searchResults.total > 0 ? (
                <>
                  {searchResults.lots.length > 0 && (
                    <div style={{ marginBottom: 8 }}>
                      <div
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          textTransform: 'uppercase',
                          color: '#6a7491',
                          padding: '4px 16px',
                          letterSpacing: 0.5,
                        }}
                      >
                        Bãi của tôi
                      </div>
                      {searchResults.lots.map((lot) => (
                        <div
                          key={lot.id}
                          onClick={() => {
                            setSearchOpen(false);
                            router.push('/owner/parking-lots');
                          }}
                          style={{
                            padding: '8px 16px',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: 10,
                            fontSize: 13,
                            color: '#0f1b3d',
                          }}
                          onMouseEnter={(e) =>
                            (e.currentTarget.style.backgroundColor = '#f3f6fb')
                          }
                          onMouseLeave={(e) =>
                            (e.currentTarget.style.backgroundColor = 'transparent')
                          }
                        >
                          <span style={{ fontWeight: 600 }}>{lot.name}</span>
                          <span style={{ fontSize: 12, color: '#6a7491' }}>
                            {lot.addr}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}

                  {searchResults.reservations.length > 0 && (
                    <div style={{ marginBottom: 8 }}>
                      <div
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          textTransform: 'uppercase',
                          color: '#6a7491',
                          padding: '4px 16px',
                          letterSpacing: 0.5,
                        }}
                      >
                        Đặt chỗ
                      </div>
                      {searchResults.reservations.map((res) => (
                        <div
                          key={res.id}
                          onClick={() => {
                            setSearchOpen(false);
                            router.push(`/owner/reservations?q=${encodeURIComponent(res.id)}`);
                          }}
                          style={{
                            padding: '8px 16px',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            fontSize: 13,
                            color: '#0f1b3d',
                          }}
                          onMouseEnter={(e) =>
                            (e.currentTarget.style.backgroundColor = '#f3f6fb')
                          }
                          onMouseLeave={(e) =>
                            (e.currentTarget.style.backgroundColor = 'transparent')
                          }
                        >
                          <div>
                            <span style={{ fontWeight: 600 }}>{res.id}</span>
                            <span style={{ color: '#6a7491', marginLeft: 8 }}>
                              {res.plate} · Ô {res.slot}
                            </span>
                          </div>
                          <span style={{ fontSize: 12, color: '#2563eb' }}>
                            {res.status}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}

                  {searchResults.spots.length > 0 && (
                    <div>
                      <div
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          textTransform: 'uppercase',
                          color: '#6a7491',
                          padding: '4px 16px',
                          letterSpacing: 0.5,
                        }}
                      >
                        Phiên đỗ xe
                      </div>
                      {searchResults.spots.map((s) => (
                        <div
                          key={s.id}
                          onClick={() => {
                            setSearchOpen(false);
                            router.push(`/owner/sessions?q=${encodeURIComponent(s.p || s.id)}`);
                          }}
                          style={{
                            padding: '8px 16px',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            fontSize: 13,
                            color: '#0f1b3d',
                          }}
                          onMouseEnter={(e) =>
                            (e.currentTarget.style.backgroundColor = '#f3f6fb')
                          }
                          onMouseLeave={(e) =>
                            (e.currentTarget.style.backgroundColor = 'transparent')
                          }
                        >
                          <div>
                            <span style={{ fontWeight: 600 }}>{s.p || s.id}</span>
                            <span style={{ color: '#6a7491', marginLeft: 8 }}>
                              Ô {s.id} (Tầng {s.f})
                            </span>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </>
              ) : (
                <div style={{ padding: '16px', textAlign: 'center', color: '#6a7491', fontSize: 13 }}>
                  Không tìm thấy kết quả
                </div>
              )}
            </div>
          )}
        </div>

        {/* Bell Notification */}
        <div ref={notifRef} style={{ position: 'relative' }}>
          <button
            onClick={() => setNotifOpen((o) => !o)}
            aria-label="Thông báo"
            style={{
              position: 'relative',
              background: '#f3f6fb',
              border: '1px solid #e3e8f2',
              borderRadius: '50%',
              width: 40,
              height: 40,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
            }}
          >
            <svg
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="#0f1b3d"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
              <path d="M13.73 21a2 2 0 0 1-3.46 0" />
            </svg>
            {unreadCount > 0 && (
              <span
                style={{
                  position: 'absolute',
                  top: -2,
                  right: -2,
                  background: '#e5484d',
                  color: '#fff',
                  fontSize: 11,
                  fontWeight: 700,
                  width: 18,
                  height: 18,
                  borderRadius: '50%',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  lineHeight: 1,
                  boxShadow: '0 2px 4px rgba(229,72,77,0.3)',
                }}
              >
                {unreadCount}
              </span>
            )}
          </button>

          {/* Notification Popover Panel */}
          {notifOpen && (
            <div
              style={{
                position: 'absolute',
                top: 'calc(100% + 8px)',
                right: 0,
                width: 380,
                background: '#fff',
                borderRadius: 16,
                boxShadow: '0 12px 32px rgba(15,27,61,0.14)',
                border: '1px solid #e3e8f2',
                zIndex: 100,
                padding: '14px 0',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '0 16px 10px',
                  borderBottom: '1px solid #e3e8f2',
                }}
              >
                <div style={{ fontWeight: 800, fontSize: 15, color: '#0f1b3d' }}>
                  Thông báo
                </div>
                {unreadCount > 0 && (
                  <button
                    onClick={() => markAllAsRead()}
                    style={{
                      border: 'none',
                      background: 'none',
                      color: '#2563eb',
                      fontSize: 12,
                      fontWeight: 600,
                      cursor: 'pointer',
                      padding: 0,
                    }}
                  >
                    Đánh dấu đã đọc
                  </button>
                )}
              </div>

              {/* Category tabs */}
              <div
                style={{
                  display: 'flex',
                  gap: 4,
                  overflowX: 'auto',
                  padding: '8px 12px',
                  borderBottom: '1px solid #f0f2f5',
                }}
              >
                {CATEGORY_TABS.map((tab) => (
                  <button
                    key={tab.key}
                    onClick={() => setActiveTab(tab.key)}
                    style={{
                      border: 'none',
                      background: activeTab === tab.key ? '#2563eb' : '#f3f6fb',
                      color: activeTab === tab.key ? '#fff' : '#6a7491',
                      borderRadius: 999,
                      padding: '4px 10px',
                      fontSize: 11.5,
                      fontWeight: 600,
                      cursor: 'pointer',
                      whiteSpace: 'nowrap',
                    }}
                  >
                    {tab.label}
                  </button>
                ))}
              </div>

              {/* Notification list */}
              <div style={{ maxHeight: 300, overflowY: 'auto' }}>
                {filteredNotifications.length > 0 ? (
                  filteredNotifications.map((n) => (
                    <div
                      key={n.id}
                      onClick={() => {
                        markAsRead(n.id);
                        if (n.href) {
                          setNotifOpen(false);
                          router.push(n.href);
                        }
                      }}
                      style={{
                        padding: '10px 16px',
                        borderBottom: '1px solid #f5f7fa',
                        cursor: 'pointer',
                        backgroundColor: n.read ? '#fff' : '#f8faff',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: 3,
                      }}
                      onMouseEnter={(e) =>
                        (e.currentTarget.style.backgroundColor = '#f0f4ff')
                      }
                      onMouseLeave={(e) =>
                        (e.currentTarget.style.backgroundColor = n.read
                          ? '#fff'
                          : '#f8faff')
                      }
                    >
                      <div
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'baseline',
                        }}
                      >
                        <span
                          style={{
                            fontWeight: n.read ? 600 : 700,
                            fontSize: 13,
                            color: '#0f1b3d',
                          }}
                        >
                          {n.title}
                        </span>
                        {!n.read && (
                          <span
                            style={{
                              width: 7,
                              height: 7,
                              borderRadius: '50%',
                              backgroundColor: '#2563eb',
                            }}
                          />
                        )}
                      </div>
                      <div style={{ fontSize: 12, color: '#6a7491', lineHeight: 1.4 }}>
                        {n.body}
                      </div>
                    </div>
                  ))
                ) : (
                  <div
                    style={{
                      padding: '24px 16px',
                      textAlign: 'center',
                      color: '#6a7491',
                      fontSize: 12.5,
                    }}
                  >
                    Không có thông báo nào trong mục này
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        {/* User Info Avatar */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div
            style={{
              width: 38,
              height: 38,
              borderRadius: '50%',
              backgroundColor: '#e3f28d',
              color: '#1a3300',
              fontWeight: 800,
              fontSize: 15,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            M
          </div>
          <div>
            <div style={{ fontSize: 13.5, fontWeight: 700, color: '#0f1b3d' }}>
              Minh
            </div>
            <div style={{ fontSize: 11.5, color: '#6a7491' }}>Chủ bãi</div>
          </div>
        </div>
      </div>
    </header>
  );
}
