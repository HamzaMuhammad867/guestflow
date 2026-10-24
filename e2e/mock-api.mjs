// Tiny stand-in for the .NET API (port 8080) so you can run the front end and E2E tests
// without SQL Server/Redis. Mirrors the real API's routes and validation rules.
import http from 'node:http';

const rooms = Array.from({ length: 40 }, (_, i) => {
  const floor = Math.floor(i / 10) + 1, n = (i % 10) + 1;
  return { number: `${floor}${String(n).padStart(2, '0')}`, status: ['Occupied', 'Available', 'Available', 'Cleaning'][n % 4] };
});
const requests = [];
let nextId = 1;
const next = { Open: ['InProgress', 'Done'], InProgress: ['Done'], Done: [] };

const send = (res, code, body) => { res.writeHead(code, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(body)); };
const readBody = (req) => new Promise((r) => { let d = ''; req.on('data', (c) => (d += c)); req.on('end', () => r(d ? JSON.parse(d) : {})); });

http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://x');
  if (req.method === 'GET' && url.pathname === '/api/rooms/availability') {
    const c = (s) => rooms.filter((r) => r.status === s).length;
    return send(res, 200, { total: rooms.length, available: c('Available'), occupied: c('Occupied'), cleaning: c('Cleaning'),
      outOfService: 0, availableRooms: rooms.filter((r) => r.status === 'Available').map((r) => r.number), generatedAtUtc: new Date().toISOString() });
  }
  if (req.method === 'GET' && url.pathname === '/api/requests') {
    const s = url.searchParams.get('status');
    return send(res, 200, [...requests].reverse().filter((r) => !s || r.status === s));
  }
  if (req.method === 'POST' && url.pathname === '/api/requests') {
    const b = await readBody(req);
    if (!rooms.some((r) => r.number === b.roomNumber))
      return send(res, 400, { errors: { roomNumber: [`Room ${b.roomNumber} does not exist.`] } });
    const r = { id: nextId++, roomNumber: b.roomNumber, category: b.category, description: b.description, status: 'Open', createdAtUtc: new Date().toISOString(), completedAtUtc: null };
    requests.push(r);
    return send(res, 201, r);
  }
  const m = url.pathname.match(/^\/api\/requests\/(\d+)\/status$/);
  if (req.method === 'PATCH' && m) {
    const r = requests.find((x) => x.id === Number(m[1]));
    if (!r) return send(res, 404, {});
    const b = await readBody(req);
    if (!next[r.status].includes(b.status)) return send(res, 409, { error: `Cannot move a request from ${r.status} to ${b.status}.` });
    r.status = b.status;
    return send(res, 200, r);
  }
  send(res, 404, {});
}).listen(8080, () => console.log('Mock API on http://localhost:8080'));
