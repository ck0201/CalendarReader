import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { App } from './App';
import * as authHook from '../features/auth/useAuth';
import * as calendarsHook from '../features/calendar/useCalendars';
import * as insightsHook from '../features/insights/useCalendarInsights';
import type { CalendarInsightResult } from '../core/types/models';

vi.mock('../features/auth/useAuth');
vi.mock('../features/calendar/useCalendars');
vi.mock('../features/insights/useCalendarInsights');

describe('App dashboard integration with TasksSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders TasksSection with tasks when user is authenticated and insights are loaded', () => {
    vi.spyOn(authHook, 'useAuth').mockReturnValue({
      isAuthenticated: true,
      isConfigured: true,
      missingConfigMessage: null,
      accounts: [
        {
          accountId: 'acc-1',
          email: 'user@example.com',
          displayName: 'Test User',
          connectedAt: '2026-06-01T00:00:00Z',
          requiresReauth: false,
        },
      ],
      loading: false,
      errorMessage: null,
      clearError: vi.fn(),
      setAlert: vi.fn(),
      alertType: 'info',
      disconnectAccount: vi.fn(),
      logout: vi.fn(),
      refreshAuth: vi.fn(),
    });

    vi.spyOn(calendarsHook, 'useCalendars').mockReturnValue({
      calendars: [
        {
          id: 'primary',
          accountId: 'acc-1',
          accountEmail: 'user@example.com',
          summary: 'Primary Calendar',
          isPrimary: true,
          accessRole: 'owner',
          isSelectedByDefault: true,
          isShared: false,
        },
      ],
      selectedCalendarIds: ['primary'],
      loading: false,
      error: null,
      toggleCalendar: vi.fn(),
      selectAll: vi.fn(),
      selectPrimaryOnly: vi.fn(),
    });

    const mockInsights: CalendarInsightResult = {
      startDate: '2026-10-05T00:00:00Z',
      endDate: '2026-10-11T23:59:59Z',
      totalEvents: 1,
      timedEventsCount: 1,
      allDayEventsCount: 0,
      totalScheduledTime: '01:00:00',
      formattedScheduledTime: '1h',
      totalMeetingTime: '01:00:00',
      formattedMeetingTime: '1h',
      longestFocusBlock: {
        duration: '02:00:00',
        formattedDuration: '2h',
        dayOfWeek: 'Tuesday',
      },
      busiestDay: {
        dayOfWeek: 'Tuesday',
        dateFormatted: 'Oct 6',
        scheduledTime: '01:00:00',
        formattedScheduledTime: '1h',
        eventCount: 1,
      },
      recurringTime: {
        duration: '00:00:00',
        formattedDuration: '0m',
        percentageOfScheduledTime: 0,
        recurringEventCount: 0,
      },
      observations: ['Tuesday is your busiest day with 1h scheduled.'],
      events: [
        {
          id: 'ev-1',
          title: 'Sprint Planning',
          start: '2026-10-06T10:00:00Z',
          end: '2026-10-06T11:00:00Z',
          isAllDay: false,
          isRecurring: false,
          attendeeCount: 1,
          isTrainJourney: false,
          accountEmail: 'user@example.com',
        },
      ],
      tasks: [
        {
          id: 't-1',
          title: 'Prepare sprint deck',
          dueDate: '2026-10-06T17:00:00Z',
          isAllDay: false,
          isCompleted: false,
          accountEmail: 'user@example.com',
        },
      ],
      scheduleHealth: {
        totalConflicts: 0,
        totalConflictDuration: '00:00:00',
        formattedConflictDuration: '0m',
        affectedEventsCount: 0,
        conflictFreeDaysCount: 7,
        mostConflictedDay: 'None',
        hasTodayConflict: false,
        conflicts: [],
        suggestions: [],
      },
    };

    vi.spyOn(insightsHook, 'useCalendarInsights').mockReturnValue({
      insights: mockInsights,
      loading: false,
      error: null,
      refetch: vi.fn(),
      conflictingEventIds: new Set(),
    });

    render(<App />);

    // Verify TasksSection is rendered within dashboard
    expect(screen.getByText('Tasks in this Period')).toBeInTheDocument();
    expect(screen.getByText('1 task')).toBeInTheDocument();
    expect(screen.getByText('Prepare sprint deck')).toBeInTheDocument();

    // Verify ScheduleHealthSection and EventsSection are also present
    expect(screen.getByText('Schedule Health & Conflicts')).toBeInTheDocument();
    expect(screen.getByText('Events in this Period')).toBeInTheDocument();
  });
});
