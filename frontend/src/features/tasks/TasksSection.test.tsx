import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TasksSection } from './TasksSection';
import type { CalendarTask } from '../../core/types/models';

const mockTasks: CalendarTask[] = [
  {
    id: 't-1',
    title: 'Prepare design doc',
    description: 'Focus on conflict detection algorithms',
    dueDate: '2026-06-01T17:00:00Z',
    isAllDay: false,
    isCompleted: false,
    accountEmail: 'user@example.com',
  },
  {
    id: 't-2',
    title: 'Review pull request',
    description: null,
    dueDate: '',
    isAllDay: false,
    isCompleted: true,
    accountEmail: 'user@example.com',
  },
];

describe('TasksSection', () => {
  it('renders tasks with titles, status, and count', () => {
    render(<TasksSection tasks={mockTasks} isMultiAccount={false} />);

    expect(screen.getByText('Tasks in this Period')).toBeInTheDocument();
    expect(screen.getByText('2 tasks')).toBeInTheDocument();
    expect(screen.getByText('Prepare design doc')).toBeInTheDocument();
    expect(screen.getByText('Review pull request')).toBeInTheDocument();
  });

  it('renders empty state when tasks list is empty', () => {
    render(<TasksSection tasks={[]} isMultiAccount={false} />);

    expect(screen.getByText('0 tasks')).toBeInTheDocument();
    expect(screen.getByText('No tasks scheduled in this period.')).toBeInTheDocument();
  });

  it('renders account email on task row when isMultiAccount is true', () => {
    const multiAccountTasks: CalendarTask[] = [
      {
        id: 't-work',
        title: 'Submit quarterly report',
        dueDate: '2026-06-01T15:00:00Z',
        isAllDay: false,
        isCompleted: false,
        accountEmail: 'work@company.com',
      },
      {
        id: 't-pers',
        title: 'Schedule dentist checkup',
        dueDate: '2026-06-02T10:00:00Z',
        isAllDay: false,
        isCompleted: true,
        accountEmail: 'personal@gmail.com',
      },
    ];

    render(<TasksSection tasks={multiAccountTasks} isMultiAccount={true} />);

    expect(screen.getByText('work@company.com')).toBeInTheDocument();
    expect(screen.getByText('personal@gmail.com')).toBeInTheDocument();
    expect(screen.getByText('Submit quarterly report')).toBeInTheDocument();
    expect(screen.getByText('Schedule dentist checkup')).toBeInTheDocument();
  });

  it('handles undefined tasks prop defensively', () => {
    render(<TasksSection isMultiAccount={false} />);

    expect(screen.getByText('0 tasks')).toBeInTheDocument();
    expect(screen.getByText('No tasks scheduled in this period.')).toBeInTheDocument();
  });
});
