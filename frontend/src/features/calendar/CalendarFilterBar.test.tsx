import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CalendarFilterBar } from './CalendarFilterBar';
import type { UserCalendar } from '../../core/types/models';

describe('CalendarFilterBar', () => {
  const mockCalendars: UserCalendar[] = [
    {
      id: 'work-primary',
      accountId: 'acc-1',
      accountEmail: 'work@company.com',
      summary: 'Work Primary',
      isPrimary: true,
      backgroundColor: '#4285F4',
      foregroundColor: '#FFFFFF',
      accessRole: 'owner',
      isSelectedByDefault: true,
      isShared: false,
    },
    {
      id: 'work-team',
      accountId: 'acc-1',
      accountEmail: 'work@company.com',
      summary: 'Engineering Sync',
      isPrimary: false,
      backgroundColor: '#9C27B0',
      foregroundColor: '#FFFFFF',
      accessRole: 'reader',
      isSelectedByDefault: false,
      isShared: true,
    },
    {
      id: 'personal-primary',
      accountId: 'acc-2',
      accountEmail: 'personal@gmail.com',
      summary: 'Personal Calendar',
      isPrimary: true,
      backgroundColor: '#0F9D58',
      foregroundColor: '#FFFFFF',
      accessRole: 'owner',
      isSelectedByDefault: true,
      isShared: false,
    },
  ];

  it('renders nothing when calendars list is empty', () => {
    const { container } = render(
      <CalendarFilterBar
        calendars={[]}
        selectedCalendarIds={[]}
        onToggleCalendar={vi.fn()}
        onSelectAll={vi.fn()}
        onSelectPrimaryOnly={vi.fn()}
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders calendars grouped by account with appropriate Primary and Shared badges', () => {
    render(
      <CalendarFilterBar
        calendars={mockCalendars}
        selectedCalendarIds={['work-primary', 'personal-primary']}
        onToggleCalendar={vi.fn()}
        onSelectAll={vi.fn()}
        onSelectPrimaryOnly={vi.fn()}
      />
    );

    expect(screen.getByText('Calendars')).toBeInTheDocument();
    expect(screen.getByText('(2 of 3 active)')).toBeInTheDocument();
    expect(screen.getByText('work@company.com')).toBeInTheDocument();
    expect(screen.getByText('personal@gmail.com')).toBeInTheDocument();

    expect(screen.getByText('Work Primary')).toBeInTheDocument();
    expect(screen.getByText('Engineering Sync')).toBeInTheDocument();
    expect(screen.getByText('Personal Calendar')).toBeInTheDocument();

    // Badges
    expect(screen.getAllByText('Primary').length).toBe(2);
    expect(screen.getByText('Shared')).toBeInTheDocument();
  });

  it('calls onToggleCalendar when a calendar checkbox is toggled', () => {
    const onToggle = vi.fn();
    render(
      <CalendarFilterBar
        calendars={mockCalendars}
        selectedCalendarIds={['work-primary', 'personal-primary']}
        onToggleCalendar={onToggle}
        onSelectAll={vi.fn()}
        onSelectPrimaryOnly={vi.fn()}
      />
    );

    // Toggle the unselected 'Engineering Sync'
    const syncCheckbox = screen.getByRole('checkbox', { name: /Engineering Sync/i });
    fireEvent.click(syncCheckbox);

    expect(onToggle).toHaveBeenCalledTimes(1);
    expect(onToggle).toHaveBeenCalledWith('work-team');
  });

  it('triggers quick action handlers for Select All and Primary Only', () => {
    const onSelectAll = vi.fn();
    const onSelectPrimaryOnly = vi.fn();

    render(
      <CalendarFilterBar
        calendars={mockCalendars}
        selectedCalendarIds={['work-primary']}
        onToggleCalendar={vi.fn()}
        onSelectAll={onSelectAll}
        onSelectPrimaryOnly={onSelectPrimaryOnly}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: /Select All/i }));
    expect(onSelectAll).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: /Primary Only/i }));
    expect(onSelectPrimaryOnly).toHaveBeenCalledTimes(1);
  });
});
