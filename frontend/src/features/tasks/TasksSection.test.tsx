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
});
