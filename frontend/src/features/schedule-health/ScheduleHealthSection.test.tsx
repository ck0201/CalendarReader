import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ScheduleHealthSection } from './ScheduleHealthSection';
import type { ScheduleHealthInfo } from '../../core/types/models';

describe('ScheduleHealthSection', () => {
  it('renders clean state when there are 0 conflicts and no suggestions', () => {
    const health: ScheduleHealthInfo = {
      totalConflicts: 0,
      totalConflictDuration: '00:00:00',
      formattedConflictDuration: '0m',
      affectedEventsCount: 0,
      conflictFreeDaysCount: 7,
      mostConflictedDay: 'None',
      hasTodayConflict: false,
      conflicts: [],
      suggestions: [],
    };

    render(<ScheduleHealthSection health={health} isMultiAccount={false} />);

    expect(screen.getByText('Schedule Health & Conflicts')).toBeInTheDocument();
    expect(screen.getByText('0 conflicts')).toBeInTheDocument();
    expect(screen.getByText('All commitments are clash-free')).toBeInTheDocument();
    expect(screen.getByText('No overlapping calendar events found in this period.')).toBeInTheDocument();
  });

  it('renders conflicts, timeline, and suggestions when conflicts exist', () => {
    const health: ScheduleHealthInfo = {
      totalConflicts: 1,
      totalConflictDuration: '00:30:00',
      formattedConflictDuration: '30m',
      affectedEventsCount: 2,
      conflictFreeDaysCount: 6,
      mostConflictedDay: 'Monday',
      hasTodayConflict: false,
      conflicts: [
        {
          id: 'test-conflict-1',
          eventA: {
            id: 'ev-1',
            title: 'Architecture Sync',
            start: '2026-06-01T10:00:00Z',
            end: '2026-06-01T11:00:00Z',
            htmlLink: null,
          },
          eventB: {
            id: 'ev-2',
            title: 'Customer Interview',
            start: '2026-06-01T10:30:00Z',
            end: '2026-06-01T11:30:00Z',
            htmlLink: null,
          },
          overlapStart: '2026-06-01T10:30:00Z',
          overlapEnd: '2026-06-01T11:00:00Z',
          overlapDuration: '00:30:00',
          formattedOverlapDuration: '30m',
          accountEmail: 'user@example.com',
        },
      ],
      suggestions: [
        {
          type: 'double_booked',
          severity: 'warning',
          title: 'Resolve Overlapping Meetings',
          description: 'You have 1 direct schedule clash on Monday, June 1.',
        },
      ],
    };

    render(<ScheduleHealthSection health={health} isMultiAccount={true} />);

    expect(screen.getAllByText('1 conflict').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Resolve Overlapping Meetings')).toBeInTheDocument();
    expect(screen.getAllByText('Architecture Sync').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Customer Interview').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/30m/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Daily Conflict Timeline')).toBeInTheDocument();
  });

  it('renders cross-account badge and individual account chips when isCrossAccount is true', () => {
    const health: ScheduleHealthInfo = {
      totalConflicts: 1,
      totalConflictDuration: '00:30:00',
      formattedConflictDuration: '30m',
      affectedEventsCount: 2,
      conflictFreeDaysCount: 6,
      mostConflictedDay: 'Tuesday',
      hasTodayConflict: false,
      crossAccountConflictsCount: 1,
      sameAccountConflictsCount: 0,
      conflicts: [
        {
          id: 'cross-conflict-1',
          eventA: {
            id: 'pers-1',
            title: 'Dentist Appointment',
            start: '2026-10-06T10:00:00Z',
            end: '2026-10-06T11:00:00Z',
            htmlLink: null,
            accountEmail: 'personal@gmail.com',
          },
          eventB: {
            id: 'work-1',
            title: 'Client Pitch',
            start: '2026-10-06T10:30:00Z',
            end: '2026-10-06T11:30:00Z',
            htmlLink: null,
            accountEmail: 'work@gmail.com',
          },
          overlapStart: '2026-10-06T10:30:00Z',
          overlapEnd: '2026-10-06T11:00:00Z',
          overlapDuration: '00:30:00',
          formattedOverlapDuration: '30m',
          accountEmail: 'Multiple Accounts',
          isCrossAccount: true,
        },
      ],
      suggestions: [
        {
          type: 'conflict',
          severity: 'warning',
          title: 'Cross-Account Double Booking',
          description: '"Dentist Appointment" (personal@gmail.com) overlaps with "Client Pitch" (work@gmail.com) by 30m.',
        },
      ],
    };

    render(<ScheduleHealthSection health={health} isMultiAccount={true} />);

    expect(screen.getByText('Cross-Account')).toBeInTheDocument();
    expect(screen.getByText('Cross-Account Double Booking')).toBeInTheDocument();
    expect(screen.getAllByText('personal@gmail.com').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('work@gmail.com').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Dentist Appointment').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Client Pitch').length).toBeGreaterThanOrEqual(1);
  });
});
