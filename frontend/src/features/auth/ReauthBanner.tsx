import React from 'react';
import type { ConnectedAccount } from '../../core/types/models';

interface ReauthBannerProps {
  accounts: ConnectedAccount[];
}

export const ReauthBanner: React.FC<ReauthBannerProps> = ({ accounts }) => {
  const reauthAccounts = accounts.filter((a) => a.requiresReauth);
  if (reauthAccounts.length === 0) return null;

  const emails = reauthAccounts.map((a) => a.email).join(', ');

  return (
    <div className="reauth-banner" role="status">
      <div>
        <strong>{emails}</strong> requires re-authorization. Reconnect to restore all events.
      </div>
      <a href="/api/auth/login?prompt=select_account" className="btn btn-subtle btn-sm">
        Reconnect
      </a>
    </div>
  );
};
