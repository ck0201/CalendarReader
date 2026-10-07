import { useCallback, useEffect, useMemo, useState } from 'react';
import { calendarApiClient } from '../../core/api/calendarApiClient';
import type { CalendarInsightResult, RangeType } from '../../core/types/models';
import { getDateRange } from '../../shared/utils/dateUtils';

export interface UseCalendarInsightsReturn {
  insights: CalendarInsightResult | null;
  loading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
  conflictingEventIds: Set<string>;
}

export function useCalendarInsights(
  range: RangeType,
  enabled: boolean,
  accountsKey?: string,
  calendarIds?: string[]
): UseCalendarInsightsReturn {
  const [insights, setInsights] = useState<CalendarInsightResult | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const calendarIdsKey = useMemo(() => {
    return calendarIds && calendarIds.length > 0 ? calendarIds.slice().sort().join(',') : '';
  }, [calendarIds]);

  const fetchInsights = useCallback(async () => {
    if (!enabled) {
      setInsights(null);
      return;
    }

    const { start, end } = getDateRange(range);
    setLoading(true);
    setError(null);

    try {
      const data = await calendarApiClient.getInsights(start, end, calendarIds);
      setInsights(data);
    } catch (err: unknown) {
      console.error('Error fetching insights:', err);
      const msg = err instanceof Error ? err.message : 'Failed to fetch calendar insights';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [range, enabled, accountsKey, calendarIdsKey]);

  useEffect(() => {
    fetchInsights();
  }, [fetchInsights]);

  const conflictingEventIds = useMemo(() => {
    const ids = new Set<string>();
    if (insights?.scheduleHealth?.conflicts) {
      for (const c of insights.scheduleHealth.conflicts) {
        if (c.eventA?.id) ids.add(c.eventA.id);
        if (c.eventB?.id) ids.add(c.eventB.id);
      }
    }
    return ids;
  }, [insights]);

  return {
    insights,
    loading,
    error,
    refetch: fetchInsights,
    conflictingEventIds,
  };
}
