import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { calendarApiClient } from '../../core/api/calendarApiClient';
import type { AiBriefingResult, RangeType } from '../../core/types/models';
import { getDateRange } from '../../shared/utils/dateUtils';

export interface UseAiBriefingReturn {
  briefing: AiBriefingResult | null;
  loading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
}

export function useAiBriefing(
  range: RangeType,
  enabled: boolean,
  accountsKey?: string,
  calendarIds?: string[]
): UseAiBriefingReturn {
  const [briefing, setBriefing] = useState<AiBriefingResult | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const calendarIdsKey = useMemo(() => {
    return calendarIds && calendarIds.length > 0 ? calendarIds.slice().sort().join(',') : '';
  }, [calendarIds]);

  const activeRequestIdRef = useRef(0);

  const fetchBriefing = useCallback(
    async (signal?: AbortSignal) => {
      if (!enabled) {
        setBriefing(null);
        setLoading(false);
        setError(null);
        return;
      }

      const requestId = ++activeRequestIdRef.current;
      const { start, end } = getDateRange(range);
      setLoading(true);
      setError(null);

      try {
        void accountsKey;
        const targetCalendarIds = calendarIdsKey ? calendarIdsKey.split(',') : undefined;
        const data = await calendarApiClient.getBriefing(start, end, targetCalendarIds, signal);
        if (requestId === activeRequestIdRef.current) {
          setBriefing(data);
        }
      } catch (err: unknown) {
        if (signal?.aborted || (err instanceof DOMException && err.name === 'AbortError')) {
          // Request was aborted due to parameter change or unmount; ignore
          return;
        }
        if (requestId === activeRequestIdRef.current) {
          console.error('Error fetching AI briefing:', err);
          const msg = err instanceof Error ? err.message : 'Failed to fetch schedule briefing';
          setError(msg);
        }
      } finally {
        if (requestId === activeRequestIdRef.current) {
          setLoading(false);
        }
      }
    },
    [range, enabled, accountsKey, calendarIdsKey]
  );

  useEffect(() => {
    const controller = new AbortController();
    fetchBriefing(controller.signal);

    return () => {
      controller.abort();
    };
  }, [fetchBriefing]);

  const refetch = useCallback(async () => {
    await fetchBriefing();
  }, [fetchBriefing]);

  return { briefing, loading, error, refetch };
}
