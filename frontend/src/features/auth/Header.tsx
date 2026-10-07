import React from 'react';
import type { ConnectedAccount } from '../../core/types/models';
import { AccountsList } from './AccountsList';

interface HeaderProps {
  isAuthenticated: boolean;
  accounts: ConnectedAccount[];
  onDisconnectAccount: (accountId: string, email: string) => void;
  onLogout: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  isAuthenticated,
  accounts,
  onDisconnectAccount,
  onLogout,
}) => {
  return (
    <header className="app-header">
      <div className="header-container">
        <div className="brand">
          <div className="logo-icon">
            <svg
              width="22"
              height="22"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
              <line x1="16" y1="2" x2="16" y2="6" />
              <line x1="8" y1="2" x2="8" y2="6" />
              <line x1="3" y1="10" x2="21" y2="10" />
              <path d="M8 14h.01" />
              <path d="M12 14h.01" />
              <path d="M16 14h.01" />
              <path d="M8 18h.01" />
              <path d="M12 18h.01" />
            </svg>
          </div>
          <span className="brand-title">CalendarReader</span>
        </div>

        {isAuthenticated && (
          <div className="auth-actions">
            <AccountsList
              accounts={accounts}
              onDisconnect={onDisconnectAccount}
            />
            <a
              href="/api/auth/login?prompt=select_account"
              className="btn btn-subtle btn-sm"
              title="Connect another Google account"
            >
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              Add Account
            </a>
            <button
              type="button"
              className="btn btn-subtle btn-sm"
              title="Disconnect all Google accounts"
              onClick={onLogout}
            >
              Disconnect All
            </button>
          </div>
        )}
      </div>
    </header>
  );
};
