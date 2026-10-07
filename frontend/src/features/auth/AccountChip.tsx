import React from 'react';
import type { ConnectedAccount } from '../../core/types/models';

interface AccountChipProps {
  account: ConnectedAccount;
  onDisconnect: (accountId: string, email: string) => void;
}

export const AccountChip: React.FC<AccountChipProps> = ({
  account,
  onDisconnect,
}) => {
  return (
    <div className={`account-chip ${account.requiresReauth ? 'reauth' : 'connected'}`}>
      <span className={`account-dot ${account.requiresReauth ? 'reauth' : ''}`} />
      <span
        className="account-email"
        title={account.displayName ? `${account.displayName} (${account.email})` : account.email}
      >
        {account.email}
      </span>
      <button
        type="button"
        className="account-disconnect-btn"
        title={`Disconnect ${account.email}`}
        aria-label={`Disconnect ${account.email}`}
        onClick={(e) => {
          e.stopPropagation();
          onDisconnect(account.accountId, account.email);
        }}
      >
        &times;
      </button>
    </div>
  );
};
