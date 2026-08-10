import type { LucideIcon } from 'lucide-react';

const iconSizes = {
  xs: 14,
  sm: 16,
  md: 18,
  lg: 20,
  xl: 24
} as const;

export type AppIconSize = keyof typeof iconSizes;

interface AppIconProps {
  icon: LucideIcon;
  size?: AppIconSize;
  className?: string;
  label?: string;
}

/**
 * The application icon boundary. Icons are decorative by default; provide a
 * label only when the icon itself conveys meaning that adjacent text does not.
 */
export function AppIcon({ icon: Icon, size = 'md', className, label }: AppIconProps) {
  return label
    ? <Icon role="img" aria-label={label} size={iconSizes[size]} strokeWidth={2} className={className}/>
    : <Icon aria-hidden="true" size={iconSizes[size]} strokeWidth={2} className={className}/>;
}
