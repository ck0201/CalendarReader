import React from 'react';

interface BadgeProps {
  children: React.ReactNode;
  variant?: 'default' | 'meeting' | 'allday' | 'recurring' | 'train' | 'account' | 'conflict';
  title?: string;
  className?: string;
}

export const Badge: React.FC<BadgeProps> = ({
  children,
  variant = 'default',
  title,
  className = '',
}) => {
  const variantClass = variant === 'default' ? 'badge' : `tag tag-${variant}`;
  return (
    <span className={`${variantClass} ${className}`.trim()} title={title}>
      {children}
    </span>
  );
};
