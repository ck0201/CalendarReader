import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useAiBriefing } from './useAiBriefing';
import { calendarApiClient } from '../../core/api/calendarApiClient';
import type { AiBriefingResult } from '../../core/types/models';

const sampleBriefing: AiBriefingResult = {
  headline: 'Weekly Schedule Overview',
  narrativeSummary: 'You have 10 hours scheduled this week.',
  preparationPriorities: ['Task A'],
  schedulingSuggestions: ['Protect Friday focus'],
  energyProfile: 'Balanced',
  generatedAtUtc: '2026-10-09T17:00:00Z',
  isCached: false,
  isFallback: false,
};

describe('useAiBriefing hook', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fetches briefing when enabled and sets briefing state', async () => {
    const getBriefingSpy = vi
      .spyOn(calendarApiClient, 'getBriefing')
      .mockResolvedValue(sampleBriefing);

    const { result } = renderHook(() =>
      useAiBriefing('week', true, 'acc-1', ['cal-1', 'cal-2'])
    );

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.briefing).toEqual(sampleBriefing);
    expect(result.current.error).toBeNull();
    expect(getBriefingSpy).toHaveBeenCalledTimes(1);

    const callArgs = getBriefingSpy.mock.calls[0];
    expect(callArgs[0]).toBeInstanceOf(Date); // start
    expect(callArgs[1]).toBeInstanceOf(Date); // end
    expect(callArgs[2]).toEqual(['cal-1', 'cal-2']); // calendarIds
  });

  it('does not fetch when enabled is false', async () => {
    const getBriefingSpy = vi.spyOn(calendarApiClient, 'getBriefing');

    const { result } = renderHook(() =>
      useAiBriefing('week', false, 'acc-1', ['cal-1'])
    );

    expect(result.current.briefing).toBeNull();
    expect(result.current.loading).toBe(false);
    expect(getBriefingSpy).not.toHaveBeenCalled();
  });

  it('triggers new request when date range changes', async () => {
    const getBriefingSpy = vi
      .spyOn(calendarApiClient, 'getBriefing')
      .mockResolvedValue(sampleBriefing);

    const { rerender } = renderHook(
      ({ range }) => useAiBriefing(range, true, 'acc-1', ['cal-1']),
      { initialProps: { range: 'week' as const } }
    );

    await waitFor(() => {
      expect(getBriefingSpy).toHaveBeenCalledTimes(1);
    });

    rerender({ range: 'today' });

    await waitFor(() => {
      expect(getBriefingSpy).toHaveBeenCalledTimes(2);
    });
  });

  it('triggers new request when selected calendars change', async () => {
    const getBriefingSpy = vi
      .spyOn(calendarApiClient, 'getBriefing')
      .mockResolvedValue(sampleBriefing);

    const { rerender } = renderHook(
      ({ cals }) => useAiBriefing('week', true, 'acc-1', cals),
      { initialProps: { cals: ['cal-1'] } }
    );

    await waitFor(() => {
      expect(getBriefingSpy).toHaveBeenCalledTimes(1);
    });

    rerender({ cals: ['cal-1', 'cal-2'] });

    await waitFor(() => {
      expect(getBriefingSpy).toHaveBeenCalledTimes(2);
    });
  });

  it('does not let an older delayed request overwrite a newer request', async () => {
    let resolveFirstRequest: (value: AiBriefingResult) => void;
    const firstPromise = new Promise<AiBriefingResult>((res) => {
      resolveFirstRequest = res;
    });

    const secondBriefing: AiBriefingResult = {
      ...sampleBriefing,
      headline: 'Newer Selection Briefing',
    };

    const getBriefingSpy = vi
      .spyOn(calendarApiClient, 'getBriefing')
      .mockImplementationOnce(() => firstPromise)
      .mockImplementationOnce(() => Promise.resolve(secondBriefing));

    const { result, rerender } = renderHook(
      ({ range }) => useAiBriefing(range, true, 'acc-1', ['cal-1']),
      { initialProps: { range: 'week' as const } }
    );

    // Change range to trigger second request immediately
    rerender({ range: 'today' });

    await waitFor(() => {
      expect(getBriefingSpy).toHaveBeenCalledTimes(2);
    });

    // Second request resolves first with secondBriefing
    await waitFor(() => {
      expect(result.current.briefing?.headline).toBe('Newer Selection Briefing');
    });

    // Now first request resolves later
    await act(async () => {
      resolveFirstRequest!({
        ...sampleBriefing,
        headline: 'Stale First Briefing',
      });
    });

    // State MUST NOT be overwritten by the stale first response!
    expect(result.current.briefing?.headline).toBe('Newer Selection Briefing');
  });

  it('manual refetch triggers a new request', async () => {
    const getBriefingSpy = vi
      .spyOn(calendarApiClient, 'getBriefing')
      .mockResolvedValue(sampleBriefing);

    const { result } = renderHook(() =>
      useAiBriefing('week', true, 'acc-1', ['cal-1'])
    );

    await waitFor(() => {
      expect(getBriefingSpy).toHaveBeenCalledTimes(1);
    });

    await act(async () => {
      await result.current.refetch();
    });

    expect(getBriefingSpy).toHaveBeenCalledTimes(2);
  });
});
