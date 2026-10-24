export type RequestCategory = 'Housekeeping' | 'Maintenance' | 'Dining' | 'Concierge';
export type RequestStatus = 'Open' | 'InProgress' | 'Done';

export interface GuestRequest {
  id: number;
  roomNumber: string;
  category: RequestCategory;
  description: string;
  status: RequestStatus;
  createdAtUtc: string;
  completedAtUtc: string | null;
}

export interface AvailabilitySummary {
  total: number;
  available: number;
  occupied: number;
  cleaning: number;
  outOfService: number;
  availableRooms: string[];
  generatedAtUtc: string;
}

async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...init,
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    const fieldErrors = body.errors ? Object.values(body.errors).flat().join(' ') : '';
    throw new Error(fieldErrors || body.error || `Request failed (${res.status})`);
  }
  return res.json() as Promise<T>;
}

export const api = {
  availability: () => http<AvailabilitySummary>('/rooms/availability'),
  listRequests: (status?: RequestStatus) =>
    http<GuestRequest[]>(`/requests${status ? `?status=${status}` : ''}`),
  createRequest: (roomNumber: string, category: RequestCategory, description: string) =>
    http<GuestRequest>('/requests', {
      method: 'POST',
      body: JSON.stringify({ roomNumber, category, description }),
    }),
  updateStatus: (id: number, status: RequestStatus) =>
    http<GuestRequest>(`/requests/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
};
