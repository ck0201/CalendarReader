import { useCallback, useEffect, useState } from 'react';
import { calendarApiClient } from '../../core/api/calendarApiClient';
import type { UserCalendar } from '../../core/types/models';

const STORAGE_KEY = 'cr_selected_calendars';

export interface UseCalendarsReturn {
  calendars: UserCalendar[];
  selectedCalendarIds: string[];
  loading: boolean;
  error: string | null;
  toggleCalendar: (id: string) => void;
  selectAll: () => void;
  selectPrimaryOnly: () => void;
}

export function useCalendars(
  isAuthenticated: boolean,
  accountsKey?: string
): UseCalendarsReturn {
  const [calendars, setCalendars] = useState<UserCalendar[]>([]);
  const [selectedCalendarIds, setSelectedCalendarIds] = useState<string[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAuthenticated) {
      setCalendars([]);
      setSelectedCalendarIds([]);
      return;
    }

    let isMounted = true;
    setLoading(true);
    setError(null);

    calendarApiClient
      .getCalendars()
      .then((data) => {
        if (!isMounted) return;
        setCalendars(data);

        // Determine initial calendar selection
        let initialIds: string[] = [];
        try {
          const saved = localStorage.getItem(STORAGE_KEY);
          if (saved) {
            const parsed = JSON.parse(saved) as string[];
            if (Array.isArray(parsed) && parsed.length > 0) {
              const availableSet = new Set(data.map((c) => c.id));
              initialIds = parsed.filter((id) => availableSet.has(id));
            }
          }
        } catch {
          // ignore localStorage errors (e.g. private browsing)
        }

        // If no saved selection exists or none matched, default to primary calendars
        if (initialIds.length === 0) {
          initialIds = data.filter((c) => c.isPrimary).map((c) => c.id);
        }

        // Fallback: if no calendar is flagged primary, select all available
        if (initialIds.length === 0) {
          initialIds = data.map((c) => c.id);
        }

        setSelectedCalendarIds(initialIds);
      })
      .catch((err) => {
        if (!isMounted) return;
        const msg = err instanceof Error ? err.message : 'Failed to retrieve calendars';
        setError(msg);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [isAuthenticated, accountsKey]);

  const toggleCalendar = useCallback(
    (id: string) => {
      setSelectedCalendarIds((prev) => {
        let next: string[];
        if (prev.includes(id)) {
          // Keep at least one calendar selected
          if (prev.length <= 1) return prev;
          next = prev.filter((calId) => calId !== id);
        } else {
          next = [...prev, id];
        }
        try {
          localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
        } catch {
          // ignore
        }
        return next;
      });
    },
    []
  );

  const selectAll = useCallback(() => {
    if (calendars.length === 0) return;
    const allIds = calendars.map((c) => c.id);
    setSelectedCalendarIds(allIds);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(allIds));
    } catch {
      // ignore
    }
  }, [calendars]);

  const selectPrimaryOnly = useCallback(() => {
    const primaryIds = calendars.filter((c) => c.isPrimary).map((c) => c.id);
    const fallback = primaryIds.length > 0 ? primaryIds : calendars.map((c) => c.id);
    setSelectedCalendarIds(fallback);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(fallback));
    } catch {
      // ignore
    }
  }, [calendars]);

  return {
    calendars,
    selectedCalendarIds,
    loading,
    error,
    toggleCalendar,
    selectAll,
    selectPrimaryOnly,
  };
}
