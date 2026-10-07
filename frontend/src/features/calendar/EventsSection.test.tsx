import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { EventsSection } from './EventsSection';
import type { CalendarEvent } from '../../core/types/models';

const mockEvents: CalendarEvent[] = [
  {
    id: 'ev-1',
    title: 'Team Standup',
    start: '2026-06-01T09:00:00Z',
    end: '2026-06-01T09:30:00Z',
    isAllDay: false,
    isRecurring: false,
    attendeeCount: 1,
    accountEmail: 'user@example.com',
    isTrainJourney: false,
  },
  {
    id: 'ev-2',
    title: '12951 NDLS TEJAS RAJ',
    start: '2026-06-01T16:55:00Z',
    end: '2026-06-02T08:30:00Z',
    isAllDay: false,
    isRecurring: false,
    attendeeCount: 1,
    accountEmail: 'user@example.com',
    isTrainJourney: true,
  },
];

describe('EventsSection', () => {
  it('renders all events by default', () => {
    const conflicted = new Set<string>();
    render(
      <EventsSection
        events={mockEvents}
        conflictingEventIds={conflicted}
        isMultiAccount={false}
      />
    );

    expect(screen.getByText('Team Standup')).toBeInTheDocument();
    expect(screen.getByText('12951 NDLS TEJAS RAJ')).toBeInTheDocument();
    expect(screen.getByText('2 events')).toBeInTheDocument();
  });

  it('renders conflict badge when event ID is in conflictedEventIds', () => {
    const conflicted = new Set<string>(['ev-1']);
    render(
      <EventsSection
        events={mockEvents}
        conflictingEventIds={conflicted}
        isMultiAccount={false}
      />
    );

    expect(screen.getByText('Conflict')).toBeInTheDocument();
  });

  it('filters to Other Events when Other tab is clicked', () => {
    const conflicted = new Set<string>();
    render(
      <EventsSection
        events={mockEvents}
        conflictingEventIds={conflicted}
        isMultiAccount={false}
      />
    );

    const otherBtn = screen.getByRole('button', { name: /other events/i });
    fireEvent.click(otherBtn);

    expect(screen.getByText('Team Standup')).toBeInTheDocument();
    expect(screen.queryByText('12951 NDLS TEJAS RAJ')).not.toBeInTheDocument();
    expect(screen.getByText('1 event')).toBeInTheDocument();
  });

  it('renders empty state when no events exist', () => {
    render(
      <EventsSection
        events={[]}
        conflictingEventIds={new Set()}
        isMultiAccount={false}
      />
    );

    expect(screen.getByText('No events scheduled in this period.')).toBeInTheDocument();
  });
});
