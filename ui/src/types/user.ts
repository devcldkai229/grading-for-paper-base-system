export const UserRole = {
  Lecturer: 0,
  Admin: 1,
} as const;

export type UserRole = typeof UserRole[keyof typeof UserRole];

export const UserStatus = {
  Active: 0,
  Inactive: 1,
  Locked: 2,
} as const;

export type UserStatus = typeof UserStatus[keyof typeof UserStatus];

export interface User {
  id: string;
  email: string;
  fullName?: string;
  phoneNumber?: string;
  markerCode?: string;
  avatarUrl?: string;
  role: UserRole;
  status: UserStatus;
  lastLoginAt?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
