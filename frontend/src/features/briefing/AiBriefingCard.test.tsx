import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AiBriefingCard } from './AiBriefingCard';
import type { AiBriefingResult } from '../../core/types/models';

const mockAiBriefing: AiBriefingResult = {
  headline: 'Productive Week Ahead',
  narrativeSummary: 'You have 14h of scheduled meetings and strong focus blocks on Wednesday.',
  preparationPriorities: ['Review architecture deck', 'Check PR backlog'],
  schedulingSuggestions: ['Protect Thursday afternoon focus window', 'Audit recurring 1:1 series'],
  energyProfile: 'Balanced — Structured schedule',
  generatedAtUtc: '2026-10-09T17:00:00Z',
  isCached: false,
  isFallback: false,
};

const mockFallbackBriefing: AiBriefingResult = {
  headline: 'Schedule Overview: 8 Events',
  narrativeSummary: 'You have 8 scheduled events totaling 12h. Longest focus block is 3h on Tuesday.',
  preparationPriorities: ['Address 1 pending task due Tuesday'],
  schedulingSuggestions: ['Maintain conflict-free days'],
  energyProfile: 'Open Focus — Flexible working blocks',
  generatedAtUtc: '2026-10-09T17:00:00Z',
  isCached: true,
  isFallback: true,
};

describe('AiBriefingCard', () => {
  it('renders all available briefing fields for an AI-generated briefing', () => {
    render(
      <AiBriefingCard
        briefing={mockAiBriefing}
        loading={false}
        error={null}
        onRetry={vi.fn()}
      />
    );

    expect(screen.getByText('AI Schedule Briefing')).toBeInTheDocument();
    expect(screen.getByText('AI Briefing')).toBeInTheDocument();
    expect(screen.getByText('Productive Week Ahead')).toBeInTheDocument();
    expect(screen.getByText(/You have 14h of scheduled meetings/)).toBeInTheDocument();
    expect(screen.getByText('Balanced — Structured schedule')).toBeInTheDocument();

    // Priorities
    expect(screen.getByText('Preparation Priorities')).toBeInTheDocument();
    expect(screen.getByText('Review architecture deck')).toBeInTheDocument();
    expect(screen.getByText('Check PR backlog')).toBeInTheDocument();

    // Suggestions
    expect(screen.getByText('Scheduling Suggestions')).toBeInTheDocument();
    expect(screen.getByText('Protect Thursday afternoon focus window')).toBeInTheDocument();
    expect(screen.getByText('Audit recurring 1:1 series')).toBeInTheDocument();
  });

  it('renders fallback result as Schedule Summary without AI-generated labels', () => {
    render(
      <AiBriefingCard
        briefing={mockFallbackBriefing}
        loading={false}
        error={null}
        onRetry={vi.fn()}
      />
    );

    // Must be labeled as Schedule Summary in both header and badge
    expect(screen.getAllByText('Schedule Summary').length).toBe(2);
    expect(screen.getByText('Schedule Overview: 8 Events')).toBeInTheDocument();

    // Must NOT have AI-Generated or AI Briefing label
    expect(screen.queryByText('AI Briefing')).not.toBeInTheDocument();
    expect(screen.queryByText('AI Schedule Briefing')).not.toBeInTheDocument();
    expect(screen.queryByText('AI-Generated')).not.toBeInTheDocument();
  });

  it('renders cached badge when isCached is true', () => {
    render(
      <AiBriefingCard
        briefing={mockFallbackBriefing}
        loading={false}
        error={null}
        onRetry={vi.fn()}
      />
    );

    expect(screen.getByText('Cached')).toBeInTheDocument();
  });

  it('renders compact loading state when loading is true', () => {
    render(
      <AiBriefingCard
        briefing={null}
        loading={true}
        error={null}
        onRetry={vi.fn()}
      />
    );

    expect(screen.getByLabelText('Loading schedule briefing')).toBeInTheDocument();
    expect(screen.getByText(/Analyzing schedule patterns/)).toBeInTheDocument();
  });

  it('renders user-friendly error message and retry button', () => {
    const handleRetry = vi.fn();
    render(
      <AiBriefingCard
        briefing={null}
        loading={false}
        error="Unable to connect to calendar service"
        onRetry={handleRetry}
      />
    );

    expect(screen.getByText('Unable to connect to calendar service')).toBeInTheDocument();
    const retryBtn = screen.getByRole('button', { name: 'Retry' });
    expect(retryBtn).toBeInTheDocument();

    fireEvent.click(retryBtn);
    expect(handleRetry).toHaveBeenCalledTimes(1);
  });

  it('renders null when briefing is null and neither loading nor error', () => {
    const { container } = render(
      <AiBriefingCard
        briefing={null}
        loading={false}
        error={null}
        onRetry={vi.fn()}
      />
    );

    expect(container.firstChild).toBeNull();
  });
});
