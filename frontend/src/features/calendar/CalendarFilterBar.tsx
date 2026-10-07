import React, { useMemo } from 'react';
import type { UserCalendar } from '../../core/types/models';

interface CalendarFilterBarProps {
  calendars: UserCalendar[];
  selectedCalendarIds: string[];
  onToggleCalendar: (id: string) => void;
  onSelectAll: () => void;
  onSelectPrimaryOnly: () => void;
  loading?: boolean;
}

export const CalendarFilterBar: React.FC<CalendarFilterBarProps> = ({
  calendars,
  selectedCalendarIds,
  onToggleCalendar,
  onSelectAll,
  onSelectPrimaryOnly,
  loading = false,
}) => {
  const selectedSet = useMemo(() => new Set(selectedCalendarIds), [selectedCalendarIds]);

  const groupedCalendars = useMemo(() => {
    const map = new Map<string, UserCalendar[]>();
    for (const cal of calendars) {
      const list = map.get(cal.accountEmail) || [];
      list.push(cal);
      map.set(cal.accountEmail, list);
    }
    return Array.from(map.entries());
  }, [calendars]);

  if (!calendars || calendars.length === 0) {
    return null;
  }

  const allSelected = calendars.length > 0 && selectedCalendarIds.length === calendars.length;
  const primaryCount = calendars.filter((c) => c.isPrimary).length;
  const isPrimaryOnly =
    primaryCount > 0 &&
    selectedCalendarIds.length === primaryCount &&
    calendars.filter((c) => c.isPrimary).every((c) => selectedSet.has(c.id));

  return (
    <div className="calendar-filter-bar">
      <div className="calendar-filter-header">
        <div className="calendar-filter-title-group">
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
          </svg>
          <span className="calendar-filter-title">Calendars</span>
          <span className="calendar-filter-count">
            ({selectedCalendarIds.length} of {calendars.length} active)
          </span>
          {loading && <span className="calendar-filter-syncing">Syncing...</span>}
        </div>

        <div className="calendar-filter-actions">
          <button
            type="button"
            className={`calendar-quick-btn ${isPrimaryOnly ? 'active' : ''}`}
            onClick={onSelectPrimaryOnly}
            title="Include only primary calendars for all accounts"
          >
            Primary Only
          </button>
          <button
            type="button"
            className={`calendar-quick-btn ${allSelected ? 'active' : ''}`}
            onClick={onSelectAll}
            title="Include all accessible calendars"
          >
            Select All
          </button>
        </div>
      </div>

      <div className="calendar-groups-list">
        {groupedCalendars.map(([email, cals]) => (
          <div key={email} className="calendar-account-group">
            <div className="calendar-group-account-label">
              <span className="account-indicator-dot" />
              <span>{email}</span>
            </div>

            <div className="calendar-chips-list">
              {cals.map((cal) => {
                const isChecked = selectedSet.has(cal.id);
                const isOnlySelected = isChecked && selectedCalendarIds.length === 1;

                return (
                  <label
                    key={cal.id}
                    className={`calendar-chip ${isChecked ? 'selected' : 'unselected'}`}
                    style={
                      isChecked && cal.backgroundColor
                        ? {
                            borderColor: cal.backgroundColor,
                            boxShadow: `0 0 0 1px ${cal.backgroundColor}30`,
                          }
                        : undefined
                    }
                  >
                    <input
                      type="checkbox"
                      checked={isChecked}
                      disabled={isOnlySelected}
                      onChange={() => onToggleCalendar(cal.id)}
                      className="calendar-chip-checkbox"
                    />

                    <span
                      className="calendar-color-dot"
                      style={{
                        backgroundColor: cal.backgroundColor || '#6366f1',
                      }}
                    />

                    <span className="calendar-chip-summary" title={cal.summary}>
                      {cal.summary}
                    </span>

                    {cal.isPrimary && (
                      <span className="badge-calendar-meta primary">Primary</span>
                    )}

                    {cal.isShared && (
                      <span className="badge-calendar-meta shared">Shared</span>
                    )}
                  </label>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
