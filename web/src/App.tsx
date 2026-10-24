import { FormEvent, useCallback, useEffect, useState } from 'react';
import { api, AvailabilitySummary, GuestRequest, RequestCategory, RequestStatus } from './api';

const CATEGORIES: RequestCategory[] = ['Housekeeping', 'Maintenance', 'Dining', 'Concierge'];
const NEXT: Partial<Record<RequestStatus, RequestStatus>> = { Open: 'InProgress', InProgress: 'Done' };
const LABEL: Record<RequestStatus, string> = { Open: 'Open', InProgress: 'In progress', Done: 'Done' };

export default function App() {
  const [summary, setSummary] = useState<AvailabilitySummary | null>(null);
  const [requests, setRequests] = useState<GuestRequest[]>([]);
  const [filter, setFilter] = useState<RequestStatus | ''>('');
  const [room, setRoom] = useState('');
  const [category, setCategory] = useState<RequestCategory>('Housekeeping');
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const refresh = useCallback(async () => {
    try {
      const [s, r] = await Promise.all([api.availability(), api.listRequests(filter || undefined)]);
      setSummary(s);
      setRequests(r);
      setError('');
    } catch (e) {
      setError((e as Error).message);
    }
  }, [filter]);

  useEffect(() => {
    refresh();
    const t = setInterval(refresh, 15000); // front-desk screens poll; the API serves this from Redis
    return () => clearInterval(t);
  }, [refresh]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await api.createRequest(room.trim(), category, description.trim());
      setRoom('');
      setDescription('');
      await refresh();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function advance(r: GuestRequest) {
    const next = NEXT[r.status];
    if (!next) return;
    try {
      await api.updateStatus(r.id, next);
      await refresh();
    } catch (err) {
      setError((err as Error).message);
    }
  }

  return (
    <main>
      <header>
        <h1>GuestFlow</h1>
        <p>Guest requests and room availability for front-desk and operations teams</p>
      </header>

      {error && <div role="alert" className="error">{error}</div>}

      <section className="tiles" aria-label="Room availability">
        <Tile label="Available" value={summary?.available} />
        <Tile label="Occupied" value={summary?.occupied} />
        <Tile label="Cleaning" value={summary?.cleaning} />
        <Tile label="Total rooms" value={summary?.total} />
      </section>

      <div className="grid">
        <section className="card">
          <h2>New request</h2>
          <form onSubmit={submit}>
            <label>
              Room number
              <input name="room" value={room} onChange={(e) => setRoom(e.target.value)} placeholder="e.g. 101" required />
            </label>
            <label>
              Category
              <select name="category" value={category} onChange={(e) => setCategory(e.target.value as RequestCategory)}>
                {CATEGORIES.map((c) => <option key={c}>{c}</option>)}
              </select>
            </label>
            <label>
              Description
              <textarea name="description" value={description} onChange={(e) => setDescription(e.target.value)}
                maxLength={500} rows={3} placeholder="Extra towels, AC not cooling…" required />
            </label>
            <button type="submit" disabled={saving}>{saving ? 'Saving…' : 'Create request'}</button>
          </form>
        </section>

        <section className="card">
          <div className="row">
            <h2>Requests</h2>
            <select aria-label="Filter by status" value={filter} onChange={(e) => setFilter(e.target.value as RequestStatus | '')}>
              <option value="">All</option>
              <option value="Open">Open</option>
              <option value="InProgress">In progress</option>
              <option value="Done">Done</option>
            </select>
          </div>
          {requests.length === 0 ? (
            <p className="muted">No requests yet.</p>
          ) : (
            <table>
              <thead>
                <tr><th>Room</th><th>Category</th><th>Description</th><th>Status</th><th /></tr>
              </thead>
              <tbody>
                {requests.map((r) => (
                  <tr key={r.id} data-testid="request-row">
                    <td>{r.roomNumber}</td>
                    <td>{r.category}</td>
                    <td>{r.description}</td>
                    <td><span className={`pill ${r.status}`}>{LABEL[r.status]}</span></td>
                    <td>
                      {NEXT[r.status] && (
                        <button className="secondary" onClick={() => advance(r)}>
                          Mark {LABEL[NEXT[r.status]!].toLowerCase()}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>
    </main>
  );
}

function Tile({ label, value }: { label: string; value?: number }) {
  return (
    <div className="tile">
      <span className="value">{value ?? '–'}</span>
      <span className="label">{label}</span>
    </div>
  );
}
