import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Header } from './Header';
import { LandingCard } from './LandingCard';
import type { ConnectedAccount } from '../../core/types/models';

describe('Header component', () => {
  it('renders branding and when not authenticated hides action buttons', () => {
    render(
      <Header
        isAuthenticated={false}
        accounts={[]}
        onDisconnectAccount={vi.fn()}
        onLogout={vi.fn()}
      />
    );

    expect(screen.getByText('CalendarReader')).toBeInTheDocument();
    expect(screen.queryByText('Add Account')).not.toBeInTheDocument();
    expect(screen.queryByText('Disconnect All')).not.toBeInTheDocument();
  });

  it('renders connected accounts and disconnect actions when authenticated', () => {
    const onLogout = vi.fn();
    const onDisconnect = vi.fn();
    const accounts: ConnectedAccount[] = [
      {
        accountId: 'acc-1',
        email: 'user1@example.com',
        displayName: 'User One',
        requiresReauth: false,
        connectedAt: '2026-06-01T00:00:00Z',
      },
      {
        accountId: 'acc-2',
        email: 'user2@example.com',
        displayName: 'User Two',
        requiresReauth: false,
        connectedAt: '2026-06-01T00:00:00Z',
      },
    ];

    render(
      <Header
        isAuthenticated={true}
        accounts={accounts}
        onDisconnectAccount={onDisconnect}
        onLogout={onLogout}
      />
    );

    expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    expect(screen.getByText('user2@example.com')).toBeInTheDocument();
    expect(screen.getByText('Add Account')).toBeInTheDocument();
    expect(screen.getByText('Disconnect All')).toBeInTheDocument();

    fireEvent.click(screen.getByText('Disconnect All'));
    expect(onLogout).toHaveBeenCalledTimes(1);
  });
});

describe('LandingCard component', () => {
  it('renders connect button pointing to /api/auth/login when configured', () => {
    render(<LandingCard isConfigured={true} onDisabledClick={vi.fn()} />);

    const link = screen.getByRole('link', { name: /connect google calendar/i });
    expect(link).toHaveAttribute('href', '/api/auth/login');
  });

  it('renders button with onDisabledClick callback when not configured', () => {
    const onDisabledClick = vi.fn();
    render(<LandingCard isConfigured={false} onDisabledClick={onDisabledClick} />);

    const button = screen.getByRole('button', { name: /connect google calendar/i });
    fireEvent.click(button);
    expect(onDisabledClick).toHaveBeenCalledTimes(1);
  });
});
